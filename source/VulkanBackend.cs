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
            return (Allocation.CreateFromValue(renderer), renderer.Instance);
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

        void IRenderingBackend.Render(in Allocation renderer, in USpan<uint> entities, in MaterialData material, in MeshData mesh, in VertexShaderData vertexShader, in FragmentShaderData fragmentShader)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.Render(entities, material, mesh, vertexShader, fragmentShader);
        }

        void IRenderingBackend.EndRender(in Allocation renderer)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.EndRender();
        }
    }
}