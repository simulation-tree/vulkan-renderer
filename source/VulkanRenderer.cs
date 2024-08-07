using Simulation;
using System;
using System.Runtime.InteropServices;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct VulkanRenderer : IRenderSystem
    {
        private static Library library;

        static FixedString IRenderSystem.Label => "vulkan";

        static unsafe (CreateFunction, DestroyFunction, RenderFunction, FinishFunction) IRenderSystem.GetFunctions()
        {
            return (new(&Create), new(&Dispose), new(&Render), new(&Finish));
        }

        [UnmanagedCallersOnly]
        private unsafe static CreateResult Create(Destination destination, nint names, int nameCount)
        {
            library = new();
            ReadOnlySpan<FixedString> namesSpan = new((void*)names, nameCount);
            Instance instance = library.CreateInstance("Game", "Engine", namesSpan);
            RendererSystem renderer = new(destination, instance);
            return CreateResult.Create(renderer, renderer.Library);
        }

        [UnmanagedCallersOnly]
        private unsafe static void Dispose(Allocation allocation)
        {
            ref RendererSystem renderer = ref allocation.AsRef<RendererSystem>();
            renderer.Dispose();
            allocation.Dispose();
        }

        [UnmanagedCallersOnly]
        private unsafe static void Render(Allocation allocation, nint surface, nint entities, int entityCount, eint material, eint mesh, eint camera)
        {
            ReadOnlySpan<eint> entitiesSpan = new((void*)entities, entityCount);
            ref RendererSystem renderer = ref allocation.AsRef<RendererSystem>();
            renderer.Render(surface, entitiesSpan, material, mesh, camera);
        }

        [UnmanagedCallersOnly]
        private static void Finish()
        {
            library.Dispose();
        }
    }
}