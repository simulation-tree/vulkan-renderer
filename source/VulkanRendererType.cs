using Rendering.Functions;
using System.Runtime.InteropServices;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct VulkanRendererType : IRenderSystem
    {
        private static Library library;

        FixedString IRenderSystem.Label => "vulkan";

        unsafe (CreateFunction, DisposeFunction, RenderFunction, FinishFunction, SurfaceCreatedFunction, SystemFunction, SystemFunction) IRenderSystem.GetFunctions()
        {
            return (new(&Create), new(&Dispose), new(&Render), new(&Finish), new(&SurfaceCreated), new(&BeginRender), new(&EndRender));
        }

        [UnmanagedCallersOnly]
        private unsafe static CreateResult Create(Destination destination, FixedString* names, uint nameCount)
        {
            library = new();
            USpan<FixedString> namesSpan = new(names, nameCount);
            Instance instance = library.CreateInstance("Game", "Engine", namesSpan);
            VulkanRenderer renderer = new(destination, instance);
            return CreateResult.Create(renderer, renderer.Library);
        }

        [UnmanagedCallersOnly]
        private unsafe static void Dispose(Allocation system)
        {
            ref VulkanRenderer renderer = ref system.Read<VulkanRenderer>();
            renderer.Dispose();
            system.Dispose();
        }

        [UnmanagedCallersOnly]
        private unsafe static uint BeginRender(Allocation system)
        {
            ref VulkanRenderer renderer = ref system.Read<VulkanRenderer>();
            return renderer.BeginRender() ? (uint)0 : 1;
        }

        [UnmanagedCallersOnly]
        private unsafe static void Render(Allocation system, uint* entities, uint entityCount, uint material, uint shader, uint mesh)
        {
            USpan<uint> entitiesSpan = new(entities, entityCount);
            ref VulkanRenderer renderer = ref system.Read<VulkanRenderer>();
            renderer.Render(entitiesSpan, material, shader, mesh);
        }

        [UnmanagedCallersOnly]
        private unsafe static uint EndRender(Allocation system)
        {
            ref VulkanRenderer renderer = ref system.Read<VulkanRenderer>();
            renderer.EndRender();
            return 0;
        }

        [UnmanagedCallersOnly]
        private static void Finish()
        {
            library.Dispose();
        }

        [UnmanagedCallersOnly]
        private static void SurfaceCreated(Allocation allocation, nint surface)
        {
            ref VulkanRenderer renderer = ref allocation.Read<VulkanRenderer>();
            renderer.SurfaceCreated(surface);
        }
    }
}