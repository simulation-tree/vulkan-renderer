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

        (MemoryAddress machine, MemoryAddress instance) IRenderingBackend.Create(in Destination destination, in ReadOnlySpan<DestinationExtension> extensionNames)
        {
            Instance instance = library.CreateInstance("Game", "Engine", extensionNames);
            VulkanRenderer machine = new(destination, instance);
            MemoryAddress instanceAddress = new(instance.Value.Handle);
            return (MemoryAddress.AllocateValue(machine), instanceAddress);
        }

        void IRenderingBackend.Dispose(in MemoryAddress machine, in MemoryAddress instance)
        {
            ref VulkanRenderer vulkanRenderer = ref machine.Read<VulkanRenderer>();
            vulkanRenderer.Dispose();
            machine.Dispose();
        }

        void IRenderingBackend.SurfaceCreated(in MemoryAddress machine, MemoryAddress surface)
        {
            ref VulkanRenderer vulkanRenderer = ref machine.Read<VulkanRenderer>();
            vulkanRenderer.SurfaceCreated(surface);
        }

        StatusCode IRenderingBackend.BeginRender(in MemoryAddress machine, in Vector4 clearColor)
        {
            ref VulkanRenderer vulkanRenderer = ref machine.Read<VulkanRenderer>();
            return vulkanRenderer.BeginRender(clearColor);
        }

        void IRenderingBackend.Render(in MemoryAddress machine, in uint materialEntity, in ushort materialVersion, in ReadOnlySpan<RenderEntity> entities)
        {
            ref VulkanRenderer vulkanRenderer = ref machine.Read<VulkanRenderer>();
            vulkanRenderer.Render(materialEntity, materialVersion, entities);
        }

        void IRenderingBackend.EndRender(in MemoryAddress machine)
        {
            ref VulkanRenderer vulkanRenderer = ref machine.Read<VulkanRenderer>();
            vulkanRenderer.EndRender();
        }
    }
}