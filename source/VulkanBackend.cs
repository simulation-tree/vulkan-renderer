using Rendering.Systems;
using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public class VulkanBackend : RenderingBackend
    {
        private static Library library;

        public override ReadOnlySpan<char> Label => "vulkan";

        public VulkanBackend()
        {
            library = new();
        }

        public override void Dispose()
        {
            library.Dispose();
        }

        public override RenderingMachine CreateRenderingMachine(Destination destination)
        {
            ReadOnlySpan<DestinationExtension> extensionNames = destination.Extensions;
            Instance vulkanInstance = library.CreateInstance("Game", "Engine", extensionNames);
            return new VulkanRenderer(destination, vulkanInstance);
        }

        public override void DisposeRenderingMachine(RenderingMachine renderingMachine)
        {
            VulkanRenderer vulkanRenderer = (VulkanRenderer)renderingMachine;
            vulkanRenderer.Dispose();
            vulkanRenderer.vulkanInstance.Dispose();
        }
    }
}