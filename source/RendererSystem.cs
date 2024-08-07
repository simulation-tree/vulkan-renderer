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
        private LogicalDevice logicalDevice;
        private Surface surface;
        private Swapchain swapchain;
        private Queue graphicsQueue;
        private Queue presentationQueue;

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
        }

        public readonly void Dispose()
        {
            foreach (ImageView imageView in surfaceImageViews)
            {
                imageView.Dispose();
            }

            surfaceImageViews.Dispose();

            if (surface != default)
            {
                swapchain.Dispose();
                surface.Dispose();
                logicalDevice.Dispose();
            }

            instance.Dispose();
            Console.WriteLine($"Vulkan instance finished for {destination.entity.value}");
        }

        public void Render(nint surfaceAddress, ReadOnlySpan<eint> entities, eint material, eint mesh, eint camera)
        {
            if (surface == default)
            {
                InitializeSurface(surfaceAddress);
            }
            //Console.WriteLine($"Rendering {entities.Length} entities using camera {camera} (destination:{destination.entity})");
        }

        private void InitializeSurface(nint surfaceAddress)
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

            Console.WriteLine($"Vulkan surface initialized for {destination.entity.value} with resolution {width}x{height}, with {imageCount} image(s)");
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