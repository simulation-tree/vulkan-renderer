using Simulation;
using System;
using Vulkan;

namespace Rendering.Vulkan
{
    internal readonly struct RendererSystem : IDisposable
    {
        private readonly Destination destination;
        private readonly Instance instance;
        private readonly PhysicalDevice physicalDevice;
        private readonly LogicalDevice logicalDevice;
        private readonly Queue graphicsQueue;

        public readonly nint Library => instance.Value.Handle;

        public RendererSystem(Destination destination, Instance instance)
        {
            this.destination = destination;
            this.instance = instance;

            if (instance.PhysicalDevices.Length == 0)
            {
                throw new InvalidOperationException("No physical devices found");
            }

            physicalDevice = FindBestPhysicalDevice(instance.PhysicalDevices);
            uint graphicsFamily = physicalDevice.GetGraphicsQueueFamily();
            logicalDevice = new(physicalDevice, [graphicsFamily]);
            graphicsQueue = logicalDevice.GetQueue(graphicsFamily, 0);
        }

        public readonly void Dispose()
        {
            logicalDevice.Dispose();
            instance.Dispose();
        }

        public readonly void Render(ReadOnlySpan<eint> entities, eint material, eint mesh, eint camera)
        {
            //Console.WriteLine($"Rendering {entities.Length} entities using camera {camera} (destination:{destination.entity})");
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