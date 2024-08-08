using System;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// References to a <see cref="LogicalDevice"/> queue.
    /// </summary>
    public readonly unsafe struct Queue
    {
        public readonly uint familyIndex;
        public readonly uint index;
        public readonly LogicalDevice logicalDevice;

        private readonly VkQueue value;

        public Queue(LogicalDevice logicalDevice, uint familyIndex, uint index)
        {
            this.index = index;
            this.familyIndex = familyIndex;
            this.logicalDevice = logicalDevice;
            vkGetDeviceQueue(logicalDevice.Value, familyIndex, index, out value);
        }

        public readonly void Submit(CommandBuffer commandBuffer, Semaphore waitSemaphore, VkPipelineStageFlags waitStage, Semaphore signalSemaphore, Fence submitFence = default)
        {
            if (waitSemaphore == default)
            {
                return;
            }

            VkSemaphore waitSemaphorePointer = waitSemaphore.Value;
            VkSemaphore signalSemaphoreValue = signalSemaphore.Value;
            VkCommandBuffer commandBufferValue = new(commandBuffer.Value);
            VkSubmitInfo submitInfo = new()
            {
                waitSemaphoreCount = 1,
                pWaitSemaphores = &waitSemaphorePointer,
                pWaitDstStageMask = &waitStage,
                commandBufferCount = 1,
                pCommandBuffers = &commandBufferValue,
                signalSemaphoreCount = 1,
                pSignalSemaphores = &signalSemaphoreValue
            };

            if (submitFence != default)
            {
                VkResult result = vkQueueSubmit(value, 1, &submitInfo, submitFence.Value);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to submit queue: {result}");
                }
            }
            else
            {
                VkResult result = vkQueueSubmit(value, 1, &submitInfo, default);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to submit queue: {result}");
                }
            }
        }

        public readonly void Submit(CommandBuffer commandBuffer, Fence submitFence = default)
        {
            VkCommandBuffer commandBufferValue = commandBuffer.Value;
            VkSubmitInfo submitInfo = new()
            {
                commandBufferCount = 1,
                pCommandBuffers = &commandBufferValue
            };

            if (submitFence != default)
            {
                VkResult result = vkQueueSubmit(value, 1, &submitInfo, submitFence.Value);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to submit queue: {result}");
                }
            }
            else
            {
                VkResult result = vkQueueSubmit(value, 1, &submitInfo, default);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to submit queue: {result}");
                }
            }
        }

        public readonly VkResult TryPresent(Semaphore signalSemaphore, Swapchain swapchain, uint imageIndex)
        {
            return vkQueuePresentKHR(value, signalSemaphore.Value, swapchain.Value, imageIndex);
        }

        public readonly void Wait()
        {
            vkQueueWaitIdle(value);
        }
    }
}
