using Meshes;
using Simulation;
using System;
using System.Runtime.InteropServices;
using Unmanaged;
using Vulkan;

namespace Rendering.Systems
{
    public readonly struct VulkanRenderer : IRenderSystem, IDisposable
    {
        private readonly World world;
        private readonly Library library;
        private readonly Instance instance;
        private readonly uint graphicsQueue;
        private readonly PhysicalDevice physicalDevice;
        private readonly LogicalDevice logicalDevice;

        public VulkanRenderer(World world, Instance instance, Library library)
        {
            this.world = world;
            this.library = library;
            this.instance = instance;

            if (instance.PhysicalDevices.Length == 0)
            {
                throw new InvalidOperationException("No physical devices found");
            }

            physicalDevice = FindBestPhysicalDevice(instance.PhysicalDevices);
            graphicsQueue = physicalDevice.GetGraphicsQueueFamily();
            logicalDevice = new(physicalDevice, [graphicsQueue]);
        }

        public void Dispose()
        {
            logicalDevice.Dispose();
            instance.Dispose();
        }

        static FixedString IRenderSystem.Label => "vulkan";

        static unsafe (SetupFunction setup, DisposeFunction tearDown, RenderFunction render) IRenderSystem.GetFunctions()
        {
            SetupFunction setup = new(&Setup);
            DisposeFunction tearDown = new(&TearDown);
            RenderFunction render = new(&Render);
            return (setup, tearDown, render);
        }

        [UnmanagedCallersOnly]
        private unsafe static void TearDown(Allocation allocation)
        {
            VulkanRenderer renderer = allocation.AsRef<VulkanRenderer>();
            renderer.Dispose();
            allocation.Dispose();
            Shared.ReturnLibrary();
        }

        [UnmanagedCallersOnly]
        private unsafe static Allocation Setup(World world, nint names, int nameCount)
        {
            Library library = Shared.TakeLibrary();
            ReadOnlySpan<FixedString> namesSpan = new((void*)names, nameCount);
            Instance instance = library.CreateInstance("Game", "Engine", namesSpan);
            VulkanRenderer renderer = new(world, instance, library);
            Allocation allocation = Allocation.Create(renderer);
            return allocation;
        }

        [UnmanagedCallersOnly]
        private unsafe static void Render(Allocation allocation, nint entities, int entityCount, Material material, Mesh mesh, Camera camera, eint destination)
        {
            VulkanRenderer renderer = allocation.AsRef<VulkanRenderer>();
            ReadOnlySpan<eint> entitiesSpan = new((void*)entities, entityCount);
            Console.WriteLine($"Rendering {entitiesSpan.Length} entities with camera {camera}");
        }

        private static PhysicalDevice FindBestPhysicalDevice(ReadOnlySpan<PhysicalDevice> physicalDevices)
        {
            uint highestScore = 0;
            int index = -1;
            for (int i = 0; i < physicalDevices.Length; i++)
            {
                uint score = GetScore(physicalDevices[i]);
                if (score > highestScore)
                {
                    highestScore = score;
                    index = i;
                }
            }

            if (index != -1)
            {
                return physicalDevices[index];
            }
            else
            {
                throw new InvalidOperationException("No suitable physical device found");
            }

            static uint GetScore(PhysicalDevice physicalDevice)
            {
                var features = physicalDevice.GetFeatures();
                if (!features.geometryShader)
                {
                    return 0;
                }

                var properties = physicalDevice.GetProperties();
                uint score = properties.limits.maxImageDimension2D;
                if (properties.deviceType == Vortice.Vulkan.VkPhysicalDeviceType.DiscreteGpu)
                {
                    score *= 10000;
                }

                return score;
            }
        }
    }
}