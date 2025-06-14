using System;
using System.Diagnostics;
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
            vkGetDeviceQueue(logicalDevice.value, familyIndex, index, out value);
        }

        public readonly void Submit(CommandBuffer commandBuffer, Semaphore waitSemaphore, VkPipelineStageFlags waitStage, Semaphore signalSemaphore, Fence submitFence = default)
        {
            if (waitSemaphore == default)
            {
                return;
            }

            VkSemaphore waitSemaphorePointer = waitSemaphore.value;
            VkSemaphore signalSemaphoreValue = signalSemaphore.value;
            VkCommandBuffer commandBufferValue = new(commandBuffer.value);
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

            VkResult result = vkQueueSubmit(value, 1, &submitInfo, submitFence.value);
            ThrowIfFailedToSubmitQueue(result);
        }

        public readonly void Submit(CommandBuffer commandBuffer, Fence submitFence = default)
        {
            VkCommandBuffer commandBufferValue = commandBuffer.value;
            VkSubmitInfo submitInfo = new()
            {
                commandBufferCount = 1,
                pCommandBuffers = &commandBufferValue
            };

            VkResult result = vkQueueSubmit(value, 1, &submitInfo, submitFence.value);
            ThrowIfFailedToSubmitQueue(result);
        }

        public readonly VkResult TryPresent(Semaphore signalSemaphore, Swapchain swapchain, uint imageIndex)
        {
            return vkQueuePresentKHR(value, signalSemaphore.value, swapchain.value, imageIndex);
        }

        /// <summary>
        /// Waits on the host for any submitted work on the queue to finish.
        /// </summary>
        public readonly void Wait()
        {
            vkQueueWaitIdle(value);
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToSubmitQueue(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to submit queue: {result}");
            }
        }
    }
}