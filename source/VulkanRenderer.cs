using Collections.Generic;
using Materials;
using Materials.Components;
using Meshes;
using Meshes.Components;
using Rendering.Components;
using Rendering.Systems;
using Shaders;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Textures;
using Textures.Components;
using Unmanaged;
using Vortice.Vulkan;
using Vulkan;
using Worlds;

namespace Rendering.Vulkan
{
    [SkipLocalsInit]
    public class VulkanRenderer : RenderingMachine
    {
        private const int MaxFramesInFlight = 2;

        public readonly Instance vulkanInstance;
        private readonly PhysicalDevice physicalDevice;
        private readonly Dictionary<(uint, uint), CompiledShader> shaders;
        private readonly Dictionary<uint, CompiledStorageBuffer> storageBuffers;
        private readonly Dictionary<uint, Array<CompiledPushConstant>> knownPushConstants;
        private readonly List<CompiledRenderer> renderers;
        private readonly Dictionary<RendererKey, CompiledPipeline> pipelines;
        private readonly List<RendererKey> pipelineKeys;
        private readonly Dictionary<RendererKey, CompiledMesh> meshes;
        private readonly List<RendererKey> meshKeys;
        private readonly Dictionary<uint, CompiledComponentBuffer> components;
        private readonly Dictionary<uint, CompiledImage> images;
        private readonly Array<CommandBuffer> commandBuffers;
        private readonly Array<Fence> inFlightFences;
        private readonly Array<Semaphore> imageAvailableSemaphores;
        private readonly Array<Semaphore> renderFinishedSemaphores;
        private readonly List<RendererCombination> previouslyRenderedGroups;
        private readonly List<uint> previouslyRenderedEntities;
        private readonly Array<Vector4> scissors;
        private readonly Stack<uint> stack;
        private readonly Array<IsTexture> textureComponents;
        private readonly int textureType;
        private readonly int materialType;
        private readonly int meshType;
        private readonly int worldRendererScissorType;
        private readonly int textureBindingType;
        private readonly int shaderVertexInputAttributeType;

        private Array<ImageView> surfaceImageViews;
        private Array<Framebuffer> swapChainFramebuffers;
        private LogicalDevice logicalDevice;
        private Surface surface;
        private Swapchain swapchain;
        private Queue graphicsQueue;
        private Queue presentationQueue;
        private RenderPass renderPass;
        private CommandPool commandPool;
        private DepthImage depthImage;
        private int currentFrame;
        private uint imageIndex;
        private uint destinationWidth;
        private uint destinationHeight;

        public VulkanRenderer(Destination destination, Instance vulkanInstance) : base(destination, new(vulkanInstance.Address))
        {
            this.vulkanInstance = vulkanInstance;

            if (vulkanInstance.PhysicalDevices.Length == 0)
            {
                throw new InvalidOperationException("No physical devices found");
            }

            if (vulkanInstance.TryGetBestPhysicalDevice(["VK_KHR_swapchain"], out physicalDevice))
            {
                Trace.WriteLine($"Vulkan instance created for `{destination}`");
            }
            else
            {
                throw new InvalidOperationException("No suitable physical device found");
            }

            images = new();
            shaders = new();
            storageBuffers = new();
            knownPushConstants = new();
            renderers = new();
            pipelines = new();
            commandBuffers = new();
            inFlightFences = new();
            imageAvailableSemaphores = new();
            renderFinishedSemaphores = new();
            previouslyRenderedGroups = new();
            previouslyRenderedEntities = new();
            pipelineKeys = new();
            meshKeys = new();
            meshes = new();
            components = new();
            scissors = new();
            stack = new();
            textureComponents = new();

            Schema schema = destination.world.Schema;
            textureType = schema.GetComponentType<IsTexture>();
            materialType = schema.GetComponentType<IsMaterial>();
            meshType = schema.GetComponentType<IsMesh>();
            worldRendererScissorType = schema.GetComponentType<WorldRendererScissor>();
            textureBindingType = schema.GetArrayType<TextureBinding>();
            shaderVertexInputAttributeType = schema.GetArrayType<ShaderVertexInputAttribute>();
        }

        /// <summary>
        /// Cleans up everything that the vulkan renderer created.
        /// </summary>
        public void Dispose()
        {
            textureComponents.Dispose();
            stack.Dispose();
            scissors.Dispose();

            if (surface != default)
            {
                logicalDevice.Wait();
                DisposeComponentBuffers();
                DisposeTextureBuffers();
                DisposePushConstants();
                DisposeRenderers();
                DisposeMeshes();
                DisposeSwapchain();
                DisposePipelines();
                DisposeShaderModules();
                DisposeInstanceBuffers();
                renderPass.Dispose();
                for (int i = 0; i < MaxFramesInFlight; i++)
                {
                    commandBuffers[i].Dispose();
                    inFlightFences[i].Dispose();
                    imageAvailableSemaphores[i].Dispose();
                    renderFinishedSemaphores[i].Dispose();
                }

                commandBuffers.Dispose();
                inFlightFences.Dispose();
                imageAvailableSemaphores.Dispose();
                renderFinishedSemaphores.Dispose();
                previouslyRenderedEntities.Dispose();
                previouslyRenderedGroups.Dispose();

                commandPool.Dispose();
                logicalDevice.Dispose();
                surface.Dispose();
            }

            Trace.WriteLine($"Vulkan instance finished for `{destination}`");
        }

        private void DisposeRenderers()
        {
            foreach (CompiledRenderer renderer in renderers)
            {
                if (renderer != default)
                {
                    renderer.Dispose();
                }
            }

            renderers.Dispose();
        }

        private void DisposePushConstants()
        {
            foreach (Array<CompiledPushConstant> pushConstantArray in knownPushConstants.Values)
            {
                pushConstantArray.Dispose();
            }

            knownPushConstants.Dispose();
        }

        private void DisposePipelines()
        {
            foreach (CompiledPipeline pipeline in pipelines.Values)
            {
                pipeline.Dispose();
            }

            pipelineKeys.Dispose();
            pipelines.Dispose();
        }

        private void DisposeShaderModules()
        {
            foreach (CompiledShader shaderModule in shaders.Values)
            {
                shaderModule.Dispose();
            }

            shaders.Dispose();
        }

        private void DisposeInstanceBuffers()
        {
            foreach (CompiledStorageBuffer instanceBuffer in storageBuffers.Values)
            {
                instanceBuffer.Dispose();
            }

            storageBuffers.Dispose();
        }

        private void DisposeComponentBuffers()
        {
            foreach (CompiledComponentBuffer componentBuffer in components.Values)
            {
                componentBuffer.Dispose();
            }

            components.Dispose();
        }

        private void DisposeTextureBuffers()
        {
            foreach (CompiledImage image in images.Values)
            {
                image.Dispose();
            }

            images.Dispose();
        }

        private void DisposeMeshes()
        {
            foreach (CompiledMesh compiledMesh in meshes.Values)
            {
                compiledMesh.Dispose();
            }

            meshes.Dispose();
            meshKeys.Dispose();
        }

        private void DisposeSwapchain()
        {
            foreach (Framebuffer framebuffer in swapChainFramebuffers)
            {
                framebuffer.Dispose();
            }

            swapChainFramebuffers.Dispose();

            foreach (ImageView imageView in surfaceImageViews)
            {
                imageView.Dispose();
            }

            surfaceImageViews.Dispose();

            if (swapchain != default)
            {
                swapchain.Dispose();
                depthImage.Dispose();
            }
        }

        private void RebuildSwapchain()
        {
            //todo: fault: should also rebuild the render pass when moving a window to hdr from sdr monitors
            logicalDevice.Wait();
            DisposeSwapchain();
            CreateSwapchain(out destinationWidth, out destinationHeight);
            CreateImageViewsAndBuffers(destinationWidth, destinationHeight);
        }

        public override void SurfaceCreated(MemoryAddress surface)
        {
            this.surface = new(vulkanInstance, surface);
            (uint graphicsFamily, uint presentationFamily) = physicalDevice.GetQueueFamilies(this.surface);
            logicalDevice = new(physicalDevice, [graphicsFamily, presentationFamily], ["VK_KHR_swapchain"]);
            graphicsQueue = new(logicalDevice, graphicsFamily, 0);
            presentationQueue = new(logicalDevice, presentationFamily, 0);
            CreateSwapchain(out destinationWidth, out destinationHeight);
            Span<RenderPass.Attachment> attachments =
            [
                new(swapchain.format, VkSampleCountFlags.Count1, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store,
                    VkAttachmentLoadOp.DontCare,
                    VkAttachmentStoreOp.DontCare, VkImageLayout.Undefined, VkImageLayout.PresentSrcKHR),
                new(logicalDevice.GetDepthFormat(), VkSampleCountFlags.Count1, VkAttachmentLoadOp.Clear,
                    VkAttachmentStoreOp.DontCare, VkAttachmentLoadOp.DontCare,
                    VkAttachmentStoreOp.DontCare, VkImageLayout.DepthStencilAttachmentOptimal,
                    VkImageLayout.DepthStencilAttachmentOptimal),
            ];

            renderPass = new(logicalDevice, attachments);
            CreateImageViewsAndBuffers(destinationWidth, destinationHeight);
            commandPool = new(graphicsQueue, true);

            //create multiples of these, 1 for each concurrent frame
            commandBuffers.Length = MaxFramesInFlight;
            inFlightFences.Length = MaxFramesInFlight;
            imageAvailableSemaphores.Length = MaxFramesInFlight;
            renderFinishedSemaphores.Length = MaxFramesInFlight;
            commandPool.CreateCommandBuffers(commandBuffers.AsSpan());

            for (int i = 0; i < MaxFramesInFlight; i++)
            {
                inFlightFences[i] = new(logicalDevice);
                imageAvailableSemaphores[i] = new(logicalDevice);
                renderFinishedSemaphores[i] = new(logicalDevice);
            }
        }

        private void CreateSwapchain(out uint width, out uint height)
        {
            if (surface.TryGetBestSize(physicalDevice, out width, out height))
            {
                swapchain = new(logicalDevice, surface, width, height);
            }
            else
            {
                (uint minWidth, uint maxWidth, uint minHeight, uint maxHeight) = surface.GetSizeRange(physicalDevice);
                (width, height) = destination.Size;
                width = Math.Max(minWidth, Math.Min(maxWidth, width));
                height = Math.Max(minHeight, Math.Min(maxHeight, height));
                swapchain = new(logicalDevice, surface, width, height);
            }

            depthImage = new(swapchain, graphicsQueue);
        }

        private void CreateImageViewsAndBuffers(uint width, uint height)
        {
            Span<Image> images = stackalloc Image[8];
            int imageCount = swapchain.CopyImagesTo(images);
            surfaceImageViews = new(imageCount);
            swapChainFramebuffers = new(imageCount);
            for (int i = 0; i < imageCount; i++)
            {
                ImageView imageView = new(images[i]);
                Framebuffer framebuffer = new(renderPass, [imageView, depthImage.imageView], width, height);
                surfaceImageViews[i] = imageView;
                swapChainFramebuffers[i] = framebuffer;
            }
        }

        private bool IsDestinationResized()
        {
            (uint width, uint height) = destination.Size;
            return width != destinationWidth || height != destinationHeight;
        }

        private CompiledShader CompileShader(uint vertexShaderEntity, uint fragmentShaderEntity, ushort vertexShaderVersion, ushort fragmentShaderVersion)
        {
            Shader vertex = Entity.Get<Shader>(world, vertexShaderEntity);
            Shader fragment = Entity.Get<Shader>(world, fragmentShaderEntity);
            ShaderModule vertexModule = new(logicalDevice, vertex.Bytes);
            ShaderModule fragmentModule = new(logicalDevice, fragment.Bytes);
            return new(vertexShaderVersion, fragmentShaderVersion, vertexModule, fragmentModule);
        }

        private CompiledMesh CompileMesh(uint meshEntity, uint vertexShaderEntity)
        {
            IsMesh mesh = world.GetComponent<IsMesh>(meshEntity, meshType);
            int vertexCount = mesh.vertexCount;
            Span<ShaderVertexInputAttribute> shaderVertexAttributes = world.GetArray<ShaderVertexInputAttribute>(vertexShaderEntity, shaderVertexInputAttributeType);
            Span<MeshChannel> channels = stackalloc MeshChannel[shaderVertexAttributes.Length];
            for (int i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ShaderVertexInputAttribute vertexAttribute = shaderVertexAttributes[i];
                if (vertexAttribute.TryDeduceMeshChannel(out MeshChannel channel))
                {
                    if (!mesh.channels.Contains(channel))
                    {
                        if (channel == MeshChannel.Color)
                        {
                            //safe to assume (1, 1, 1, 1) is default for colors if needed and its missing
                            Span<Vector4> defaultColors = world.CreateArray<MeshVertexColor>(meshEntity, vertexCount).AsSpan<Vector4>();
                            for (int v = 0; v < vertexCount; v++)
                            {
                                defaultColors[v] = new(1, 1, 1, 1);
                            }
                        }
                        else if (channel == MeshChannel.Normal)
                        {
                            Span<Vector3> defaultNormals = world.CreateArray<MeshVertexNormal>(meshEntity, vertexCount).AsSpan<Vector3>();
                            for (int v = 0; v < vertexCount; v++)
                            {
                                defaultNormals[v] = Vector3.Zero;
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException($"Mesh entity `{meshEntity}` is missing required `{channel}` channel");
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unable to deduce the mesh channel from property name `{vertexAttribute.name}`, name is too ambiguous");
                }

                channels[i] = channel;
            }

            int vertexSize = channels.GetVertexSize();
            using Array<float> vertexData = new(vertexCount * vertexSize);
            Mesh.Assemble(world, meshEntity, vertexData.AsSpan(), channels);
            Span<uint> indices = world.GetArray<MeshVertexIndex>(meshEntity).As<uint>();
            int indexCount = indices.Length;
            VertexBuffer vertexBuffer = new(graphicsQueue, commandPool, vertexData.AsSpan());
            IndexBuffer indexBuffer = new(graphicsQueue, commandPool, indices);
            //Trace.WriteLine($"Compiled mesh `{meshEntity}` with `{vertexCount}` vertices and `{indexCount}` indices");
            return new(mesh.version, (uint)indexCount, vertexBuffer, indexBuffer, shaderVertexAttributes);
        }

        private CompiledPipeline CompilePipeline(uint materialEntity, uint vertexShaderEntity, uint fragmentShaderEntity, CompiledShader compiledShader, CompiledMesh compiledMesh)
        {
            Span<ShaderVertexInputAttribute> shaderVertexAttributes = compiledMesh.VertexAttributes;
            Span<VkVertexInputAttributeDescription> vertexAttributes = stackalloc VkVertexInputAttributeDescription[shaderVertexAttributes.Length];
            uint offset = 0;
            for (int i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ref ShaderVertexInputAttribute shaderVertexAttribute = ref shaderVertexAttributes[i];
                ref VkVertexInputAttributeDescription vulkanVertexAttribute = ref vertexAttributes[i];
                vulkanVertexAttribute.location = shaderVertexAttribute.location;
                vulkanVertexAttribute.format = shaderVertexAttribute.type.GetFormat();
                vulkanVertexAttribute.binding = shaderVertexAttribute.binding;
                vulkanVertexAttribute.offset = offset;
                offset += shaderVertexAttribute.type.Size;
            }

            Material material = Entity.Get<Material>(world, materialEntity);
            ReadOnlySpan<PushConstantBinding> pushBindings = material.PushConstants;
            ReadOnlySpan<EntityComponentBinding> uniformBindings = material.ComponentBindings;
            ReadOnlySpan<TextureBinding> textureBindings = material.TextureBindings;
            ReadOnlySpan<StorageBufferBinding> storageBufferBindings = material.StorageBuffers;
            Span<ShaderPushConstant> pushConstants = world.GetArray<ShaderPushConstant>(vertexShaderEntity);
            Span<ShaderUniformProperty> uniformProperties = world.GetArray<ShaderUniformProperty>(vertexShaderEntity);
            Span<ShaderSamplerProperty> samplerProperties = world.GetArray<ShaderSamplerProperty>(fragmentShaderEntity);
            Span<ShaderStorageBuffer> vertexStorageBuffers = world.GetArray<ShaderStorageBuffer>(vertexShaderEntity);

            //collect information to build the set layout
            int totalCount = uniformBindings.Length + textureBindings.Length + vertexStorageBuffers.Length;
            Span<DescriptorSetLayoutBinding> setLayoutBindings = stackalloc DescriptorSetLayoutBinding[totalCount];
            int setLayoutBindingCount = 0;
            Span<PipelineLayout.PushConstant> pushConstantsBuffer = stackalloc PipelineLayout.PushConstant[pushConstants.Length];
            int pushConstantsCount = 0;

            //cant have more than 1 push constant of the same type, so batch them into 1 vertex push constant
            //todo: fault: ^^^ what if theres fragment push constants? or geometry push constants? this will break
            if (pushConstants.Length > 0)
            {
                uint start = 0;
                uint size = 0;
                for (int c = 0; c < pushConstants.Length; c++)
                {
                    ShaderPushConstant pushConstant = pushConstants[c];
                    start = Math.Min(start, pushConstant.offset);
                    size += pushConstant.size;
                    bool containsPush = false;
                    for (int p = 0; p < pushBindings.Length; p++)
                    {
                        PushConstantBinding pushBinding = pushBindings[p];
                        ushort componentSize = pushBinding.componentType.size;
                        if (componentSize == pushConstant.size && pushBinding.start == pushConstant.offset)
                        {
                            containsPush = true;
                            break;
                        }
                    }

                    if (!containsPush)
                    {
                        throw new InvalidOperationException($"Material `{material}` is missing a `{typeof(PushConstantBinding)}` to bind a push constant named `{pushConstant.memberName}`");
                    }
                }

                pushConstantsBuffer[pushConstantsCount++] = new(start, size, VkShaderStageFlags.Vertex);
            }

            for (int p = 0; p < uniformProperties.Length; p++)
            {
                ShaderUniformProperty uniformProperty = uniformProperties[p];
                bool containsBinding = false;
                for (int b = 0; b < uniformBindings.Length; b++)
                {
                    EntityComponentBinding uniformBinding = uniformBindings[b];
                    VkShaderStageFlags shaderStage = uniformBinding.stage.GetShaderStage();
                    if (uniformBinding.key.Binding == uniformProperty.binding && uniformBinding.key.Set == uniformProperty.set)
                    {
                        containsBinding = true;
                        DescriptorSetLayoutBinding binding = new(uniformBinding.key.Binding, VkDescriptorType.UniformBuffer, 1, shaderStage);
                        setLayoutBindings[setLayoutBindingCount++] = binding;
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{material}` is missing a `{typeof(EntityComponentBinding).Name}` to bind a component to property at `{uniformProperty.name}`({uniformProperty.binding})");
                }
            }

            for (int p = 0; p < samplerProperties.Length; p++)
            {
                ShaderSamplerProperty samplerProperty = samplerProperties[p];
                bool containsBinding = false;
                for (int b = 0; b < textureBindings.Length; b++)
                {
                    TextureBinding textureBinding = textureBindings[b];
                    if (textureBinding.key.Binding == samplerProperty.binding && textureBinding.key.Set == samplerProperty.set)
                    {
                        containsBinding = true;
                        DescriptorSetLayoutBinding binding = new(textureBinding.key.Binding, VkDescriptorType.CombinedImageSampler, 1, VkShaderStageFlags.Fragment);
                        setLayoutBindings[setLayoutBindingCount++] = binding;
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{material}` is missing a `{typeof(TextureBinding).Name}` to bind a texture to property at `{samplerProperty.name}`({samplerProperty.binding})");
                }
            }

            for (int s = 0; s < vertexStorageBuffers.Length; s++)
            {
                ShaderStorageBuffer storageBuffer = vertexStorageBuffers[s];
                bool containsBinding = false;
                for (int b = 0; b < storageBufferBindings.Length; b++)
                {
                    StorageBufferBinding storageBufferBinding = storageBufferBindings[b];
                    VkShaderStageFlags shaderStage = storageBufferBinding.stage.GetShaderStage();
                    if (storageBufferBinding.key.Binding == storageBuffer.binding && storageBufferBinding.key.Set == storageBuffer.set)
                    {
                        containsBinding = true;
                        DescriptorSetLayoutBinding binding = new(storageBuffer.binding, VkDescriptorType.StorageBuffer, 1, shaderStage);
                        setLayoutBindings[setLayoutBindingCount++] = binding;
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{material}` is missing a `{typeof(StorageBufferBinding).Name}` to bind a storage buffer to property at `{storageBuffer.name}`({storageBuffer.binding})");
                }
            }

            //create pipeline
            DescriptorSetLayout setLayout = new(logicalDevice, setLayoutBindings.Slice(0, setLayoutBindingCount));
            PipelineCreateInput pipelineCreation = new(renderPass, compiledShader.vertexShader, compiledShader.fragmentShader);
            IsMaterial component = material.GetComponent<IsMaterial>(materialType);
            pipelineCreation.blendSettings = component.blendSettings;
            pipelineCreation.depthSettings = component.depthSettings;

            Span<VertexInputBindingDescription> vertexBindings = stackalloc VertexInputBindingDescription[1];
            vertexBindings[0] = new(0, offset, VkVertexInputRate.Vertex);
            //if (compiledShader.isInstanced)
            //{
            //    //vertexBindings[1] = new(instanceSize, VkVertexInputRate.Instance, 1);
            //}

            PipelineLayout pipelineLayout = new(logicalDevice, setLayout, pushConstantsBuffer.Slice(0, pushConstantsCount));

            //todo: find the exact entry point string from the shader
            Pipeline pipeline = new(pipelineCreation, pipelineLayout, vertexBindings, vertexAttributes, "main");

            //create descriptor pool
            Span<DescriptorPoolSize> poolSizes = stackalloc DescriptorPoolSize[8];
            int poolCount = 0;
            if (uniformProperties.Length > 0)
            {
                poolSizes[poolCount++] = new(VkDescriptorType.UniformBuffer, (uint)uniformProperties.Length);
            }

            if (samplerProperties.Length > 0)
            {
                poolSizes[poolCount++] = new(VkDescriptorType.CombinedImageSampler, (uint)samplerProperties.Length);
            }

            if (vertexStorageBuffers.Length > 0)
            {
                poolSizes[poolCount++] = new(VkDescriptorType.StorageBuffer, (uint)vertexStorageBuffers.Length);
            }

            //remember which bindings are push constants
            if (!knownPushConstants.TryGetValue(materialEntity, out Array<CompiledPushConstant> pushConstantArray))
            {
                pushConstantArray = new();
                knownPushConstants.Add(materialEntity, pushConstantArray);
            }

            if (pushBindings.Length > 0)
            {
                Span<CompiledPushConstant> buffer = stackalloc CompiledPushConstant[pushBindings.Length];
                for (int i = 0; i < pushBindings.Length; i++)
                {
                    PushConstantBinding binding = pushBindings[i];
                    buffer[i] = new(binding.componentType, binding.stage, binding.stage.GetShaderStage());
                }

                pushConstantArray.Length = buffer.Length;
                pushConstantArray.CopyFrom(buffer);
            }

            //create buffers for bindings that arent push constants (referring to components on entities)
            VkPhysicalDeviceLimits limits = logicalDevice.physicalDevice.GetLimits();
            for (int b = 0; b < uniformBindings.Length; b++)
            {
                EntityComponentBinding binding = uniformBindings[b];
                uint componentEntity = binding.entity;
                DataType dataType = binding.componentType;
                if (!world.ContainsEntity(componentEntity))
                {
                    throw new InvalidOperationException($"Material `{material}` references missing entity `{componentEntity}` for component `{dataType.ToString(world.Schema)}`");
                }

                if (!world.ContainsComponent(componentEntity, dataType.index))
                {
                    throw new InvalidOperationException($"Material `{material}` references entity `{componentEntity}` for a missing component `{dataType.ToString(world.Schema)}`");
                }

                uint componentHash = GetComponentHash(material.value, binding);
                if (!components.TryGetValue(componentHash, out CompiledComponentBuffer componentBuffer))
                {
                    ushort componentSize = dataType.size;
                    uint byteLength = (uint)(Math.Ceiling(componentSize / (float)limits.minUniformBufferOffsetAlignment) * limits.minUniformBufferOffsetAlignment);
                    VkBufferUsageFlags usage = VkBufferUsageFlags.UniformBuffer;
                    VkMemoryPropertyFlags propertyFlags = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent;
                    BufferDeviceMemory buffer = new(logicalDevice, byteLength, usage, propertyFlags);
                    componentBuffer = new(material, binding.entity, dataType, buffer);
                    components.Add(componentHash, componentBuffer);
                }
            }

            //create buffers for texture bindings
            for (int b = 0; b < textureBindings.Length; b++)
            {
                TextureBinding binding = textureBindings[b];
                uint textureEntity = binding.Entity;
                if (!world.ContainsEntity(textureEntity))
                {
                    throw new InvalidOperationException($"Material `{material}` references texture entity `{textureEntity}`, which does not exist");
                }

                IsTexture textureComponent = textureComponents[(int)textureEntity];
                if (textureComponent == default)
                {
                    throw new InvalidOperationException($"Material `{material}` references entity `{textureEntity}` that doesn't qualify as a texture");
                }

                uint textureHash = GetTextureHash(materialEntity, binding);
                if (!images.TryGetValue(textureHash, out CompiledImage compiledImage))
                {
                    compiledImage = CompileImage(materialEntity, binding, textureComponent);
                    images.Add(textureHash, compiledImage);
                }
            }

            return new(pipeline, pipelineLayout, poolSizes.Slice(0, poolCount), setLayout, setLayoutBindings.Slice(0, setLayoutBindingCount));
        }

        private CompiledImage CompileImage(uint materialEntity, TextureBinding binding, IsTexture component)
        {
            uint depth = 1;
            VkImageUsageFlags usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
            //VkFormat format = VkFormat.R8G8B8A8Srgb; //todo: why is this commented out again? i forget = gamma
            VkFormat format = VkFormat.R8G8B8A8Unorm;
            uint textureEntity = binding.Entity;
            bool isCubemap = world.ContainsTag<IsCubemapTexture>(textureEntity);
            Vector4 region = binding.Region;
            uint x = (uint)(region.X * component.width);
            uint y = (uint)(region.Y * component.height);
            uint z = (uint)(region.Z * component.width);
            uint w = (uint)(region.W * component.height);
            uint minX = Math.Min(x, z);
            uint minY = Math.Min(y, w);
            uint maxX = Math.Max(x, z);
            uint maxY = Math.Max(y, w);
            uint width = maxX - minX;
            uint height = maxY - minY;
            Image image = new(logicalDevice, width, height, depth, format, usage, isCubemap);
            DeviceMemory imageMemory = new(image, VkMemoryPropertyFlags.DeviceLocal);
            Values<Pixel> pixels = world.GetArray<Pixel>(textureEntity);
            uint layerCount = isCubemap ? 6u : 1u;

            //copy pixels from the entity, into the temporary buffer, then temporary buffer copies into the buffer... yada yada yada
            using BufferDeviceMemory tempStagingBuffer = new(logicalDevice, (uint)pixels.Length * 4, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostCoherent | VkMemoryPropertyFlags.HostVisible);
            tempStagingBuffer.memory.CopyFrom(pixels.AsSpan());
            VkImageLayout imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
            using CommandPool tempPool = new(graphicsQueue, true);
            using CommandBuffer tempBuffer = tempPool.CreateCommandBuffer();
            tempBuffer.Begin();
            tempBuffer.TransitionImageLayout(image, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, VkImageAspectFlags.Color, layerCount);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();
            tempBuffer.Begin();
            tempBuffer.CopyBufferToImage(tempStagingBuffer.buffer, (uint)component.width, (uint)component.height, minX, minY, image, depth, layerCount);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();
            tempBuffer.Begin();
            tempBuffer.TransitionImageLayout(image, VkImageLayout.TransferDstOptimal, imageLayout, VkImageAspectFlags.Color, layerCount);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();

            ImageView imageView = new(image, VkImageAspectFlags.Color, isCubemap);
            SamplerCreateParameters samplerParameters = new();
            samplerParameters.minFilter = binding.Filter == TextureFiltering.Linear ? VkFilter.Linear : VkFilter.Nearest;
            samplerParameters.magFilter = samplerParameters.minFilter;
            Sampler sampler = new(logicalDevice, samplerParameters);
            Trace.WriteLine($"Compiled image for material `{materialEntity}` with `{width}`x`{height}` pixels (cubemap: {isCubemap})");
            return new(materialEntity, component.version, binding, image, imageView, imageMemory, sampler);
        }

        /// <summary>
        /// Copies data from components into the uniform buffers for material bindings.
        /// </summary>
        private void UpdateComponentBuffers()
        {
            foreach (CompiledComponentBuffer componentBuffer in components.Values)
            {
                uint entity = componentBuffer.targetEntity;
                DataType dataType = componentBuffer.componentType;
                if (!world.ContainsEntity(entity))
                {
                    throw new InvalidOperationException($"Entity `{entity}` that contained component `{dataType.ToString(world.Schema)}` with data for a uniform buffer has been lost");
                }

                if (!world.ContainsComponent(entity, dataType.index))
                {
                    throw new InvalidOperationException($"Component `{dataType.ToString(world.Schema)}` on entity `{entity}` that used to contained data for a uniform buffer has been lost");
                }

                MemoryAddress component = world.GetComponent(entity, dataType.index, out int componentSize);
                componentBuffer.buffer.memory.CopyFrom(component, componentSize);
            }
        }

        /// <summary>
        /// Rebuilds textures for still used materials when their source updates.
        /// </summary>
        private void UpdateTextureBuffers(Span<IsTexture> textureComponentsSpan)
        {
            foreach (uint textureHash in images.Keys)
            {
                ref CompiledImage image = ref images[textureHash];
                Material material = Entity.Get<Material>(world, image.materialEntity);
                if (material.TryGetFirstTextureBinding(image.binding.Entity, out TextureBinding binding))
                {
                    IsTexture component = textureComponentsSpan[(int)binding.Entity];
                    if (image.textureVersion != component.version)
                    {
                        //todo: untested: (triggered when the texture's pixel array changes)
                        logicalDevice.Wait();
                        image.Dispose();
                        image = CompileImage(image.materialEntity, binding, component);
                    }
                }
            }
        }

        public override bool BeginRender(Vector4 clearColor)
        {
            ref Fence submitFence = ref inFlightFences[currentFrame];
            ref CommandBuffer commandBuffer = ref commandBuffers[currentFrame];

            submitFence.Wait();

            VkResult result = logicalDevice.TryAcquireNextImage(swapchain, imageAvailableSemaphores[currentFrame], default, out imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                RebuildSwapchain();
                return false;
            }
            else if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                throw new InvalidOperationException($"Failed to acquire next image: {result}");
            }

            submitFence.Reset();
            commandBuffer.Reset();
            commandBuffer.Begin();

            ref Framebuffer framebuffer = ref swapChainFramebuffers[(int)imageIndex];
            Vector4 area = new(0, 0, framebuffer.width, framebuffer.height);
            commandBuffer.BeginRenderPass(renderPass, framebuffer, area, clearColor);

            Vector4 viewport = new(0, framebuffer.height, framebuffer.width, -framebuffer.height);
            commandBuffer.SetViewport(viewport);

            Vector4 scissor = new(0, 0, framebuffer.width, framebuffer.height);
            commandBuffer.SetScissor(scissor);

            int capacity = (world.MaxEntityValue + 1).GetNextPowerOf2();
            if (textureComponents.Length < capacity)
            {
                textureComponents.Length = capacity;
            }

            textureComponents.Clear();

            CollectComponents(textureComponents.AsSpan());
            UpdateComponentBuffers();
            UpdateTextureBuffers(textureComponents.AsSpan());
            ReadScissorValues(area);
            return true;
        }

        private void CollectComponents(Span<IsTexture> textureComponentsSpan)
        {
            ReadOnlySpan<Chunk> chunks = world.Chunks;
            for (int c = 0; c < chunks.Length; c++)
            {
                Chunk chunk = chunks[c];
                if (chunk.Definition.ContainsComponent(textureType))
                {
                    ComponentEnumerator<IsTexture> components = chunk.GetComponents<IsTexture>(textureType);
                    ReadOnlySpan<uint> entities = chunk.Entities;
                    for (int i = 0; i < entities.Length; i++)
                    {
                        textureComponentsSpan[(int)entities[i]] = components[i];
                    }
                }
            }
        }

        private void ReadScissorValues(Vector4 area)
        {
            int capacity = (world.MaxEntityValue + 1).GetNextPowerOf2();
            if (scissors.Length < capacity)
            {
                scissors.Length = capacity;
            }

            scissors.Fill(area);
            stack.Clear(capacity);

            Span<Vector4> scissorsSpan = scissors.AsSpan();
            ReadOnlySpan<Chunk> chunks = world.Chunks;
            Span<Chunk> chunksWithScissors = stackalloc Chunk[world.CountChunksWithComponent(worldRendererScissorType)];
            int chunkCount = 0;
            for (int c = 0; c < chunks.Length; c++)
            {
                Chunk chunk = chunks[c];
                if (chunk.Definition.ContainsComponent(worldRendererScissorType))
                {
                    chunksWithScissors[chunkCount++] = chunk;
                }
            }

            //propagate scissors down to descendants
            for (int c = 0; c < chunkCount; c++)
            {
                Chunk chunk = chunksWithScissors[c];
                ReadOnlySpan<uint> entities = chunk.Entities;
                ComponentEnumerator<WorldRendererScissor> components = chunk.GetComponents<WorldRendererScissor>(worldRendererScissorType);
                for (int i = 0; i < entities.Length; i++)
                {
                    ref WorldRendererScissor scissor = ref components[i];
                    uint entity = entities[i];
                    scissorsSpan[(int)entity] = scissor.value;

                    stack.Push(entity);
                    while (stack.Count > 0)
                    {
                        uint current = stack.Pop();
                        Iterate(world, stack, scissorsSpan, scissor.value, current);
                    }

                    static void Iterate(World world, Stack<uint> stack, Span<Vector4> scissors, Vector4 scissor, uint entity)
                    {
                        int childCount = world.GetChildCount(entity);
                        if (childCount > 0)
                        {
                            Span<uint> children = stackalloc uint[childCount];
                            world.CopyChildrenTo(entity, children);
                            for (int i = 0; i < childCount; i++)
                            {
                                scissors[(int)children[i]] = scissor;
                            }

                            stack.PushRange(children);
                        }
                    }
                }
            }

            //hard assign the roots back to the original scissor
            for (int c = 0; c < chunkCount; c++)
            {
                Chunk chunk = chunksWithScissors[c];
                ReadOnlySpan<uint> entities = chunk.Entities;
                ComponentEnumerator<WorldRendererScissor> components = chunk.GetComponents<WorldRendererScissor>(worldRendererScissorType);
                for (int i = 0; i < entities.Length; i++)
                {
                    ref WorldRendererScissor scissor = ref components[i];
                    uint entity = entities[i];
                    scissorsSpan[(int)entity] = scissor.value;
                }
            }
        }

        public override void Render(sbyte renderGroup, ReadOnlySpan<RenderEntity> renderEntities)
        {
            bool deviceWaited = false;

            void TryWait(LogicalDevice logicalDevice)
            {
                if (!deviceWaited)
                {
                    deviceWaited = true;
                    logicalDevice.Wait();
                }
            }

            //ensure enough capacity to store all compiled renderers
            uint maxEntityPosition = 0;
            for (int i = 0; i < renderEntities.Length; i++)
            {
                uint entity = renderEntities[i].entity;
                if (entity > maxEntityPosition)
                {
                    maxEntityPosition = entity;
                }
            }

            if (maxEntityPosition >= renderers.Count)
            {
                int toAdd = (int)maxEntityPosition - renderers.Count + 1;
                renderers.AddDefault(toAdd);
            }

            //build and render everything in one go
            Span<IsTexture> textureComponentsSpan = textureComponents.AsSpan();
            ref CommandBuffer commandBuffer = ref commandBuffers[currentFrame];
            Span<CompiledRenderer> renderersSpan = renderers.AsSpan();
            Span<Vector4> scissorsSpan = scissors.AsSpan();
            ReadOnlySpan<Slot> slots = world.Slots;
            for (int i = 0; i < renderEntities.Length; i++)
            {
                RenderEntity renderEntity = renderEntities[i];
                uint entity = renderEntity.entity;
                Slot slot = slots[(int)entity];

                //deal with missing or outdated shaders
                uint vertexShaderEntity = renderEntity.vertexShaderEntity;
                uint fragmentShaderEntity = renderEntity.fragmentShaderEntity;
                ushort vertexShaderVersion = renderEntity.vertexShaderVersion;
                ushort fragmentShaderVersion = renderEntity.fragmentShaderVersion;
                bool shaderVersionChanged = false;
                ref CompiledShader compiledShader = ref shaders.TryGetValue((vertexShaderEntity, fragmentShaderEntity), out bool containsShader);
                if (!containsShader)
                {
                    compiledShader = ref shaders.Add((vertexShaderEntity, fragmentShaderEntity));
                    compiledShader = CompileShader(vertexShaderEntity, fragmentShaderEntity, vertexShaderVersion, fragmentShaderVersion);
                }
                else
                {
                    shaderVersionChanged = compiledShader.vertexVersion != vertexShaderVersion;
                    if (shaderVersionChanged)
                    {
                        TryWait(logicalDevice);
                        compiledShader.Dispose();
                        compiledShader = CompileShader(vertexShaderEntity, fragmentShaderEntity, vertexShaderVersion, fragmentShaderVersion);
                    }
                }

                //deal with missing or outdated meshes
                uint meshEntity = renderEntity.meshEntity;
                ushort meshVersion = renderEntity.meshVersion;
                RendererKey key = new(renderEntity.materialEntity, meshEntity);
                bool meshVersionChanged = false;
                ref CompiledMesh compiledMesh = ref meshes.TryGetValue(key, out bool containsMesh);
                if (!containsMesh)
                {
                    compiledMesh = ref meshes.Add(key);
                    compiledMesh = CompileMesh(meshEntity, vertexShaderEntity);
                    meshKeys.Add(key);
                }
                else
                {
                    meshVersionChanged = compiledMesh.version != meshVersion;
                    if (meshVersionChanged || shaderVersionChanged)
                    {
                        TryWait(logicalDevice);
                        compiledMesh.Dispose();
                        compiledMesh = CompileMesh(meshEntity, vertexShaderEntity);
                    }
                }

                //deal with missing or outdated material
                bool textureBindingsChanged = false;
                Span<TextureBinding> textureBindings = world.GetArray<TextureBinding>(renderEntity.materialEntity, textureBindingType);
                for (int t = 0; t < textureBindings.Length; t++)
                {
                    ref TextureBinding textureBinding = ref textureBindings[t];
                    uint textureHash = GetTextureHash(renderEntity.materialEntity, textureBinding);
                    ref CompiledImage compiledImage = ref images.TryGetValue(textureHash, out bool containsImage);
                    if (containsImage)
                    {
                        if (compiledImage.binding.Version != textureBinding.Version || compiledImage.binding.Region != textureBinding.Region)
                        {
                            TryWait(logicalDevice);
                            compiledImage.Dispose();

                            IsTexture component = textureComponentsSpan[(int)textureBinding.Entity];
                            compiledImage = CompileImage(renderEntity.materialEntity, textureBinding, component);
                            textureBindingsChanged = true;
                        }
                    }
                }

                //deal with missing pipelines
                uint materialEntity = renderEntity.materialEntity;
                ref CompiledPipeline compiledPipeline = ref pipelines.TryGetValue(key, out bool containsPipeline);
                if (!containsPipeline)
                {
                    //todo: pipeline should be rebuilt if the mesh attribute layout changes
                    Trace.WriteLine($"Creating pipeline for material `{materialEntity}` and mesh `{meshEntity}` for the first time");
                    compiledPipeline = ref pipelines.Add(key);
                    compiledPipeline = CompilePipeline(materialEntity, vertexShaderEntity, fragmentShaderEntity, compiledShader, compiledMesh);
                    pipelineKeys.Add(key);
                }

                //deal with missing or outdated renderers
                ref CompiledRenderer compiledRenderer = ref renderersSpan[(int)entity];
                if (containsPipeline && compiledRenderer != default)
                {
                    if (textureBindingsChanged || shaderVersionChanged)
                    {
                        compiledRenderer.Dispose();
                        compiledRenderer = default;
                    }
                }

                if (compiledRenderer == default)
                {
                    DescriptorSet descriptorSet = compiledPipeline.Allocate();
                    UpdateDescriptorSet(materialEntity, descriptorSet, compiledPipeline);
                    compiledRenderer = new(descriptorSet);
                }

                //finally submit the command to draw
                commandBuffer.BindPipeline(compiledPipeline.pipeline, VkPipelineBindPoint.Graphics);
                commandBuffer.BindVertexBuffer(compiledMesh.vertexBuffer);
                commandBuffer.BindIndexBuffer(compiledMesh.indexBuffer);

                //apply scissor
                commandBuffer.SetScissor(scissorsSpan[(int)entity]);

                //push constants
                if (knownPushConstants.TryGetValue(materialEntity, out Array<CompiledPushConstant> pushConstants))
                {
                    int pushOffset = 0;
                    Span<CompiledPushConstant> pushConstantsSpan = pushConstants.AsSpan();
                    for (int p = 0; p < pushConstantsSpan.Length; p++)
                    {
                        ref CompiledPushConstant pushConstant = ref pushConstantsSpan[p];
                        DataType componentType = pushConstant.componentType;
                        MemoryAddress component = slot.GetComponent(componentType.index);
                        commandBuffer.PushConstants(compiledPipeline.pipelineLayout, pushConstant.stageFlags, component, pushConstant.componentType.size, (uint)pushOffset);
                        pushOffset += pushConstant.componentType.size;
                    }
                }

                commandBuffer.BindDescriptorSet(compiledPipeline.pipelineLayout, compiledRenderer.descriptorSet);
                commandBuffer.DrawIndexed(compiledMesh.indexCount, 1, 0, 0, 0);

                previouslyRenderedEntities.Add(entity);
                previouslyRenderedGroups.TryAdd(new(materialEntity, meshEntity, vertexShaderEntity, fragmentShaderEntity));
            }
        }

        public override void EndRender()
        {
            ref Semaphore signalSemaphore = ref renderFinishedSemaphores[currentFrame];
            ref Semaphore waitSemaphore = ref imageAvailableSemaphores[currentFrame];
            ref CommandBuffer commandBuffer = ref commandBuffers[currentFrame];

            commandBuffer.EndRenderPass();
            commandBuffer.End();

            graphicsQueue.Submit(commandBuffer, waitSemaphore, VkPipelineStageFlags.ColorAttachmentOutput, signalSemaphore, inFlightFences[currentFrame]);

            VkResult result = presentationQueue.TryPresent(signalSemaphore, swapchain, imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || IsDestinationResized())
            {
                RebuildSwapchain();
            }
            else if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Failed to present image: {result}");
            }

            currentFrame = (currentFrame + 1) % MaxFramesInFlight;
            DisposeUnusued();
        }

        private void DisposeUnusued()
        {
            bool waited = false;

            //dispose unusued buffers
            Span<uint> toRemove = stackalloc uint[512]; //todo: this can crash if not enough space
            int removeCount = 0;
            Span<RendererCombination> previouslyRenderedGroups = this.previouslyRenderedGroups.AsSpan();
            Span<uint> previouslyRenderedEntities = this.previouslyRenderedEntities.AsSpan();
            Span<RendererKey> pipelineKeys = this.pipelineKeys.AsSpan();
            Span<RendererKey> meshKeys = this.meshKeys.AsSpan();
            Span<CompiledRenderer> renderers = this.renderers.AsSpan();
            foreach ((uint componentHash, CompiledComponentBuffer component) in components)
            {
                bool used = false;
                for (int c = 0; c < previouslyRenderedGroups.Length; c++)
                {
                    if (previouslyRenderedGroups[c].materialEntity == component.material.value)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    toRemove[removeCount++] = componentHash;
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (int i = 0; i < removeCount; i++)
                {
                    components.Remove(toRemove[i], out CompiledComponentBuffer component);
                    component.Dispose();
                }

                removeCount = 0;
            }

            //dispose unused textures
            foreach ((uint textureHash, CompiledImage image) in images)
            {
                bool used = false;
                for (int c = 0; c < previouslyRenderedGroups.Length; c++)
                {
                    if (previouslyRenderedGroups[c].materialEntity == image.materialEntity)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    toRemove[removeCount++] = textureHash;
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (int i = 0; i < removeCount; i++)
                {
                    images.Remove(toRemove[i], out CompiledImage image);
                    image.Dispose();
                }

                removeCount = 0;
            }

            //dispose unused renderers
            for (uint e = 1; e < renderers.Length; e++)
            {
                if (renderers[(int)e] != default)
                {
                    if (!previouslyRenderedEntities.Contains(e))
                    {
                        toRemove[removeCount++] = e;
                    }
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (int i = 0; i < removeCount; i++)
                {
                    ref CompiledRenderer renderer = ref renderers[(int)toRemove[i]];
                    renderer.Dispose();
                    renderer = default;
                }

                removeCount = 0;
            }

            //dispose unused meshes
            Span<RendererKey> toRemoveKeys = stackalloc RendererKey[Math.Max(pipelineKeys.Length, meshKeys.Length)];
            for (int i = 0; i < meshKeys.Length; i++)
            {
                RendererKey key = meshKeys[i];
                bool used = false;
                for (int c = 0; c < previouslyRenderedGroups.Length; c++)
                {
                    if (previouslyRenderedGroups[c].Key == key.value)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    toRemoveKeys[removeCount++] = key;
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (int i = 0; i < removeCount; i++)
                {
                    RendererKey key = toRemoveKeys[i];
                    if (meshes.TryRemove(key, out CompiledMesh mesh))
                    {
                        this.meshKeys.TryRemoveBySwapping(key);
                        mesh.Dispose();
                    }
                }

                removeCount = 0;
            }

            //dispose unused pipelines
            for (int i = 0; i < pipelineKeys.Length; i++)
            {
                RendererKey key = pipelineKeys[i];
                bool used = false;
                for (int c = 0; c < previouslyRenderedGroups.Length; c++)
                {
                    if (previouslyRenderedGroups[c].Key == key.value)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    toRemoveKeys[removeCount++] = key;
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (int i = 0; i < removeCount; i++)
                {
                    RendererKey key = toRemoveKeys[i];
                    if (pipelines.TryRemove(key, out CompiledPipeline pipeline))
                    {
                        this.pipelineKeys.TryRemoveBySwapping(key);
                        pipeline.Dispose();
                    }
                }

                removeCount = 0;
            }

            this.previouslyRenderedGroups.Clear();
            this.previouslyRenderedEntities.Clear();
        }

        private void UpdateDescriptorSet(uint materialEntity, DescriptorSet descriptorSet, CompiledPipeline pipeline)
        {
            Material material = Entity.Get<Material>(world, materialEntity);
            byte set = 0;
            Span<DescriptorSetLayoutBinding> descriptorBindings = pipeline.DescriptorBindings;
            for (int b = 0; b < descriptorBindings.Length; b++)
            {
                DescriptorSetLayoutBinding descriptorBinding = descriptorBindings[b];
                uint binding = descriptorBinding.binding;
                DescriptorResourceKey key = new((byte)binding, set);
                if (descriptorBinding.descriptorType == VkDescriptorType.CombinedImageSampler)
                {
                    TextureBinding textureBinding = material.GetTextureBinding(key);
                    uint textureHash = GetTextureHash(materialEntity, textureBinding);
                    ref CompiledImage image = ref images[textureHash];
                    descriptorSet.Update(image.imageView, image.sampler, descriptorBinding.descriptorType, binding);
                }
                else if (descriptorBinding.descriptorType == VkDescriptorType.UniformBuffer)
                {
                    EntityComponentBinding componentBinding = material.GetComponentBinding(key, ShaderType.Vertex);
                    uint componentHash = GetComponentHash(materialEntity, componentBinding);
                    ref CompiledComponentBuffer componentBuffer = ref components[componentHash];
                    descriptorSet.Update(componentBuffer.buffer.buffer, descriptorBinding.descriptorType, binding);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported descriptor type `{descriptorBinding.descriptorType}`");
                }
            }
        }

        private static uint GetTextureHash(uint materialEntity, TextureBinding binding)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)materialEntity;
                hash = hash * 31 + binding.key.GetHashCode();
                hash = hash * 31 + (int)binding.Entity;
                return (uint)hash;
            }
        }

        private static uint GetComponentHash(uint materialEntity, EntityComponentBinding binding)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)materialEntity;
                hash = hash * 31 + binding.GetHashCode();
                return (uint)hash;
            }
        }
    }
}