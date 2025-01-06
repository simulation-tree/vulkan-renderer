using Simulation;
using System.Numerics;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly partial struct VulkanBackend : IRenderingBackend
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
            VulkanRenderer renderer = new(destination, instance);
            return (Allocation.Create(renderer), renderer.Instance);
        }

        void IRenderingBackend.Dispose(in Allocation renderer)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.Dispose();
            renderer.Dispose();
        }

        void IRenderingBackend.SurfaceCreated(in Allocation renderer, Allocation surface)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.SurfaceCreated(surface);
        }

        StatusCode IRenderingBackend.BeginRender(in Allocation renderer, in Vector4 clearColor)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            return vulkanRenderer.BeginRender(clearColor);
        }

        void IRenderingBackend.Render(in Allocation renderer, in USpan<uint> entities, in uint material, in uint shader, in uint mesh)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.Render(entities, material, shader, mesh);
        }

        void IRenderingBackend.EndRender(in Allocation renderer)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.EndRender();
        }
    }
}