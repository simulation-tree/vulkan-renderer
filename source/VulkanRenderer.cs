using Meshes;
using Simulation;
using System;
using System.Runtime.InteropServices;
using Unmanaged;
using Vulkan;

namespace Rendering.Systems
{
    public readonly struct VulkanRenderer : IRenderSystem
    {
        private static Library library;

        static FixedString IRenderSystem.Label => "vulkan";

        static unsafe (SetupFunction setup, DisposeFunction tearDown, RenderFunction render) IRenderSystem.GetFunctions()
        {
            SetupFunction setup = new(&Setup);
            DisposeFunction tearDown = new(&TearDown);
            RenderFunction render = new(&Render);
            return (setup, tearDown, render);
        }

        [UnmanagedCallersOnly]
        private unsafe static void TearDown(nint address)
        {
            Allocation allocation = new((void*)address);
            Instance instance = allocation.AsRef<Instance>();
            instance.Dispose();
            allocation.Dispose();

            Shared.ReturnLibrary();
        }

        [UnmanagedCallersOnly]
        private unsafe static nint Setup(nint names, int nameCount)
        {
            ReadOnlySpan<FixedString> namesSpan = new((void*)names, nameCount);
            library = Shared.TakeLibrary();
            
            Instance instance = library.CreateInstance("Game", "Engine", namesSpan);
            Allocation allocation = Allocation.Create(instance);
            return allocation.Address;
        }

        [UnmanagedCallersOnly]
        private unsafe static void Render(World world, nint address, nint entities, int entityCount, Material material, Mesh mesh, Camera camera, eint destination)
        {
            Allocation allocation = new((void*)address);
            Instance instance = allocation.AsRef<Instance>();
            ReadOnlySpan<eint> entitiesSpan = new((void*)entities, entityCount);
            Console.WriteLine($"Rendering {entitiesSpan.Length} entities with camera {camera}");
        }
    }
}