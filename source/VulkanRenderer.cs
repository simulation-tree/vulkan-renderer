using System;
using Unmanaged;
using Vulkan;

namespace Rendering.Systems
{
    public class VulkanRenderer : IRenderSystem
    {
        private Instance instance;

        static FixedString IRenderSystem.Label => "vulkan";

        public VulkanRenderer()
        {
        }

        void IDisposable.Dispose()
        {
            instance.Dispose();
        }

        void IRenderSystem.Initialize(ReadOnlySpan<FixedString> extensionNames)
        {
            instance = Shared.library.CreateInstance("Game", "Engine", extensionNames);
        }

        void IRenderSystem.Render(Destination destination, Camera camera, ReadOnlySpan<Renderer> entities)
        {

        }
    }
}