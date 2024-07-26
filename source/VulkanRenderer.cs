using System;
using Unmanaged;
using Vulkan;

namespace Rendering.Systems
{
    public class VulkanRenderer : IRenderSystem
    {
        private Instance instance;

        public FixedString Label => "vulkan";

        public void Dispose()
        {
            instance.Dispose();
        }

        void IRenderSystem.Initialize(ReadOnlySpan<FixedString> extensionNames)
        {
            instance = new Instance("Game", "Engine", extensionNames);
        }

        void IRenderSystem.CreateInstance(Destination destination)
        {
        }
    }
}