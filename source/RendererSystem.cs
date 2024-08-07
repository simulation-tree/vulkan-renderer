using Rendering.Components;
using Shaders.Components;
using Simulation;
using System;
using Unmanaged;
using Unmanaged.Collections;
using Vulkan;

namespace Rendering.Vulkan
{
    internal struct RendererSystem : IDisposable
    {
        private readonly Destination destination;
        private readonly Instance instance;
        private readonly PhysicalDevice physicalDevice;
        private readonly UnmanagedArray<ImageView> surfaceImageViews;
        private readonly UnmanagedDictionary<eint, ShaderModule> shaderModules;
        private readonly UnmanagedDictionary<int, Pipeline> pipelines;
        private LogicalDevice logicalDevice;
        private Surface surface;
        private Swapchain swapchain;
        private Queue graphicsQueue;
        private Queue presentationQueue;
        private RenderPass renderPass;

        public readonly nint Library => instance.Value.Handle;

        public RendererSystem(Destination destination, Instance instance)
        {
            this.destination = destination;
            this.instance = instance;

            if (instance.PhysicalDevices.Length == 0)
            {
                throw new InvalidOperationException("No physical devices found");
            }

            if (TryGetBestPhysicalDevice(instance.PhysicalDevices, ["VK_KHR_swapchain"], out uint index))
            {
                physicalDevice = instance.PhysicalDevices[(int)index];
                Console.WriteLine($"Vulkan instance created for {destination.entity.value}");
            }
            else
            {
                throw new InvalidOperationException("No suitable physical device found");
            }

            surfaceImageViews = new();
            shaderModules = new();
            pipelines = new();
        }

        public readonly void Dispose()
        {
            DisposePipelines();
            DisposeShaderModules();
            DisposeSurfaceImageViews();

            if (surface != default)
            {
                renderPass.Dispose();
                swapchain.Dispose();
                surface.Dispose();
                logicalDevice.Dispose();
            }

            instance.Dispose();
            Console.WriteLine($"Vulkan instance finished for {destination.entity.value}");
        }

        private readonly void DisposePipelines()
        {
            foreach (int hash in pipelines.Keys)
            {
                Pipeline pipeline = pipelines[hash];
                pipeline.Dispose();
            }

            pipelines.Dispose();
        }

        private readonly void DisposeSurfaceImageViews()
        {
            foreach (ImageView imageView in surfaceImageViews)
            {
                imageView.Dispose();
            }

            surfaceImageViews.Dispose();
        }

        private readonly void DisposeShaderModules()
        {
            foreach (eint shaderEntity in shaderModules.Keys)
            {
                ShaderModule shaderModule = shaderModules[shaderEntity];
                shaderModule.Dispose();
            }

            shaderModules.Dispose();
        }

        public void SurfaceCreated(nint surfaceAddress)
        {
            surface = new(instance, surfaceAddress);
            (uint graphicsFamily, uint presentationFamily) = physicalDevice.GetQueueFamilies(surface);
            logicalDevice = new(physicalDevice, [graphicsFamily, presentationFamily], ["VK_KHR_swapchain"]);
            graphicsQueue = new(logicalDevice, graphicsFamily, 0);
            presentationQueue = new(logicalDevice, presentationFamily, 0);

            if (surface.TryGetBestSize(physicalDevice, out uint width, out uint height))
            {
                swapchain = new(logicalDevice, surface, width, height);
            }
            else
            {
                (uint minWidth, uint maxWidth, uint minHeight, uint maxHeight) = surface.GetSizeRange(physicalDevice);
                (width, height) = destination.GetDestinationSize();
                width = Math.Max(minWidth, Math.Min(maxWidth, width));
                height = Math.Max(minHeight, Math.Min(maxHeight, height));
                swapchain = new(logicalDevice, surface, width, height);
            }

            Span<Image> images = stackalloc Image[8];
            int imageCount = swapchain.CopyImagesTo(images);
            surfaceImageViews.Resize((uint)imageCount);
            for (int i = 0; i < imageCount; i++)
            {
                surfaceImageViews[(uint)i] = new(images[i]);
            }

            renderPass = new(logicalDevice, surface);
            Console.WriteLine($"Vulkan surface initialized for {destination.entity.value} with resolution {width}x{height}, with {imageCount} image(s)");
        }

        public void Render(ReadOnlySpan<eint> entities, eint material, eint mesh, eint camera)
        {
            //Console.WriteLine($"Rendering {entities.Length} entities using camera {camera} (destination:{destination.entity})");
            World world = destination.entity.world;
            IsMaterial materialComponent = world.GetComponent<IsMaterial>(material);
            eint shader = materialComponent.shader;
            IsShader shaderComponent = world.GetComponent<IsShader>(shader);

            //todo: detect when shader has been modified, could use a version number instead of a boolean
            if (!shaderModules.TryGetValue(shaderComponent.vertex, out ShaderModule vertexShader))
            {
                UnmanagedList<byte> bytecode = world.GetList<byte>(shaderComponent.vertex);
                vertexShader = new(logicalDevice, bytecode.AsSpan());
                shaderModules.Add(shaderComponent.vertex, vertexShader);
            }

            if (!shaderModules.TryGetValue(shaderComponent.fragment, out ShaderModule fragmentShader))
            {
                UnmanagedList<byte> bytecode = world.GetList<byte>(shaderComponent.fragment);
                fragmentShader = new(logicalDevice, bytecode.AsSpan());
                shaderModules.Add(shaderComponent.fragment, fragmentShader);
            }

            //todo: mesh hash isnt as good as the vertex/fragment, if mesh is actually a compiled object then it can work
            int hash = HashCode.Combine(vertexShader, fragmentShader, mesh);
            if (!pipelines.TryGetValue(hash, out Pipeline pipeline))
            {
                ReadOnlySpan<DescriptorSetLayout> setLayouts = stackalloc DescriptorSetLayout[0];
                ReadOnlySpan<VertexInputAttribute> vertexAttributes = stackalloc VertexInputAttribute[0];
                PipelineCreateInput pipelineCreation = new(setLayouts, renderPass, vertexShader, fragmentShader, vertexAttributes);
                pipeline = new(pipelineCreation, "main");
                pipelines.Add(hash, pipeline);
            }
        }

        private static bool TryGetBestPhysicalDevice(ReadOnlySpan<PhysicalDevice> physicalDevices, ReadOnlySpan<FixedString> requiredExtensions, out uint index)
        {
            uint highestScore = 0;
            index = uint.MaxValue;
            for (int i = 0; i < physicalDevices.Length; i++)
            {
                uint score = GetScore(physicalDevices[i], requiredExtensions);
                if (score > highestScore)
                {
                    highestScore = score;
                    index = (uint)i;
                }
            }

            return true;

            unsafe static uint GetScore(PhysicalDevice physicalDevice, ReadOnlySpan<FixedString> requiredExtensions)
            {
                Vortice.Vulkan.VkPhysicalDeviceFeatures features = physicalDevice.GetFeatures();
                if (!features.geometryShader)
                {
                    //no geometry shader support
                    return 0;
                }

                if (!physicalDevice.TryGetGraphicsQueueFamily(out _))
                {
                    //no ability to render
                    return 0;
                }

                ReadOnlySpan<Vortice.Vulkan.VkExtensionProperties> availableExtensions = physicalDevice.GetExtensions();
                if (availableExtensions.Length > 0)
                {
                    foreach (FixedString requiredExtension in requiredExtensions)
                    {
                        bool isAvailable = false;
                        foreach (Vortice.Vulkan.VkExtensionProperties extension in availableExtensions)
                        {
                            FixedString extensionName = new(extension.extensionName);
                            if (extensionName == requiredExtension)
                            {
                                isAvailable = true;
                                break;
                            }
                        }

                        if (!isAvailable)
                        {
                            //required extensions missing
                            return 0;
                        }
                    }
                }
                else if (requiredExtensions.Length > 0)
                {
                    //required extensions missing
                    return 0;
                }

                Vortice.Vulkan.VkPhysicalDeviceProperties properties = physicalDevice.GetProperties();
                uint score = properties.limits.maxImageDimension2D;
                if (properties.deviceType == Vortice.Vulkan.VkPhysicalDeviceType.DiscreteGpu)
                {
                    //discrete gpus greatly preferred
                    score *= 1024;
                }

                return score;
            }
        }
    }
}