using Simulation;
using System;
using System.Runtime.InteropServices;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct VulkanRendererType : IRenderSystem
    {
        private static Library library;

        static FixedString IRenderSystem.Label => "vulkan";

        static unsafe (CreateFunction, DisposeFunction, RenderFunction, FinishFunction, SurfaceCreatedFunction, SystemFunction, SystemFunction) IRenderSystem.GetFunctions()
        {
            return (new(&Create), new(&Dispose), new(&Render), new(&Finish), new(&SurfaceCreated), new(&BeginRender), new(&EndRender));
        }

        [UnmanagedCallersOnly]
        private unsafe static CreateResult Create(Destination destination, nint names, int nameCount)
        {
            library = new();
            ReadOnlySpan<FixedString> namesSpan = new((void*)names, nameCount);
            Instance instance = library.CreateInstance("Game", "Engine", namesSpan);
            VulkanRenderer renderer = new(destination, instance);
            Allocation buffer = new(8, true);
            return CreateResult.Create(renderer, buffer, renderer.Library);
        }

        [UnmanagedCallersOnly]
        private unsafe static void Dispose(Allocation system)
        {
            ref VulkanRenderer renderer = ref system.Read<VulkanRenderer>();
            renderer.Dispose();
            system.Dispose();
        }

        [UnmanagedCallersOnly]
        private unsafe static void BeginRender(Allocation system, Allocation buffer)
        {
            ref VulkanRenderer renderer = ref system.Read<VulkanRenderer>();
            renderer.BeginRender(buffer);
        }

        [UnmanagedCallersOnly]
        private unsafe static void Render(Allocation system, nint entities, int entityCount, eint material, eint mesh, eint camera)
        {
            ReadOnlySpan<eint> entitiesSpan = new((void*)entities, entityCount);
            ref VulkanRenderer renderer = ref system.Read<VulkanRenderer>();
            renderer.Render(entitiesSpan, material, mesh, camera);
        }

        [UnmanagedCallersOnly]
        private unsafe static void EndRender(Allocation system, Allocation buffer)
        {
            ref VulkanRenderer renderer = ref system.Read<VulkanRenderer>();
            renderer.EndRender(buffer);
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