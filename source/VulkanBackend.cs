using Simulation;
using System;
using System.Numerics;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly partial struct VulkanBackend : IRenderingBackend
    {
        private static Library library;

        readonly ASCIIText256 IRenderingBackend.Label => "vulkan";

        void IRenderingBackend.Finish()
        {
            library.Dispose();
        }

        void IRenderingBackend.Start()
        {
            library = new();
        }

        (MemoryAddress renderer, MemoryAddress instance) IRenderingBackend.Create(in Destination destination, in ReadOnlySpan<DestinationExtension> extensionNames)
        {
            Instance instance = library.CreateInstance("Game", "Engine", extensionNames);
            VulkanRenderer renderer = new(destination, instance);
            return (MemoryAddress.AllocateValue(renderer), renderer.Instance);
        }

        void IRenderingBackend.Dispose(in MemoryAddress renderer)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.Dispose();
            renderer.Dispose();
        }

        void IRenderingBackend.SurfaceCreated(in MemoryAddress renderer, MemoryAddress surface)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.SurfaceCreated(surface);
        }

        StatusCode IRenderingBackend.BeginRender(in MemoryAddress renderer, in Vector4 clearColor)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            return vulkanRenderer.BeginRender(clearColor);
        }

        void IRenderingBackend.Render(in MemoryAddress renderer, in ReadOnlySpan<uint> entities, in MaterialData material, in MeshData mesh, in VertexShaderData vertexShader, in FragmentShaderData fragmentShader)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.Render(entities, material, mesh, vertexShader, fragmentShader);
        }

        void IRenderingBackend.EndRender(in MemoryAddress renderer)
        {
            ref VulkanRenderer vulkanRenderer = ref renderer.Read<VulkanRenderer>();
            vulkanRenderer.EndRender();
        }
    }
}