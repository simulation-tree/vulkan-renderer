using Simulation;
using System.Numerics;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly partial struct VulkanRenderer : IRenderingBackend
    {
        private static Library library;

        readonly FixedString IRenderingBackend.Label => "vulkan";

        void IRenderingBackend.Finalize()
        {
            library.Dispose();
        }

        void IRenderingBackend.Initialize()
        {
            library = new();
        }

        (Allocation renderer, Allocation instance) IRenderingBackend.Create(in Destination destination, in USpan<FixedString> extensionNames)
        {
            Instance instance = library.CreateInstance("Game", "Engine", extensionNames);
            VulkanRendererSystem renderer = new(destination, instance);
            return (Allocation.Create(renderer), renderer.Instance);
        }

        void IRenderingBackend.Dispose(in Allocation renderer)
        {
            ref VulkanRendererSystem vulkanRenderer = ref renderer.Read<VulkanRendererSystem>();
            vulkanRenderer.Dispose();
            renderer.Dispose();
        }

        void IRenderingBackend.SurfaceCreated(in Allocation renderer, Allocation surface)
        {
            ref VulkanRendererSystem vulkanRenderer = ref renderer.Read<VulkanRendererSystem>();
            vulkanRenderer.SurfaceCreated(surface);
        }

        StatusCode IRenderingBackend.BeginRender(in Allocation renderer, in Vector4 clearColor)
        {
            ref VulkanRendererSystem vulkanRenderer = ref renderer.Read<VulkanRendererSystem>();
            return vulkanRenderer.BeginRender(clearColor);
        }

        void IRenderingBackend.Render(in Allocation renderer, in USpan<uint> entities, in uint material, in uint shader, in uint mesh)
        {
            ref VulkanRendererSystem vulkanRenderer = ref renderer.Read<VulkanRendererSystem>();
            vulkanRenderer.Render(entities, material, shader, mesh);
        }

        void IRenderingBackend.EndRender(in Allocation renderer)
        {
            ref VulkanRendererSystem vulkanRenderer = ref renderer.Read<VulkanRendererSystem>();
            vulkanRenderer.EndRender();
        }
    }
}