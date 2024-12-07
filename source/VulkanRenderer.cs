using Rendering.Functions;
using System.Numerics;
using System.Runtime.InteropServices;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public unsafe readonly struct VulkanRenderer : IRenderingBackend
    {
        private static Library library;

        readonly FixedString IRenderingBackend.Label => "vulkan";
        readonly CreateFunction IRenderingBackend.Create => new(&Create);
        readonly DisposeFunction IRenderingBackend.Dispose => new(&CleanUp);
        readonly FinishRenderer IRenderingBackend.Finish => new(&Finish);
        readonly SurfaceCreatedFunction IRenderingBackend.SurfaceCreated => new(&SurfaceCreated);
        readonly BeginRenderFunction IRenderingBackend.BeginRender => new(&BeginRender);
        readonly RenderFunction IRenderingBackend.Render => new(&Render);
        readonly EndRenderFunction IRenderingBackend.EndRender => new(&EndRender);

        [UnmanagedCallersOnly]
        private unsafe static CreateResult Create(Destination destination, FixedString* names, uint nameCount)
        {
            library = new();
            USpan<FixedString> namesSpan = new(names, nameCount);
            Instance instance = library.CreateInstance("Game", "Engine", namesSpan);
            VulkanRendererSystem renderer = new(destination, instance);
            return CreateResult.Create(renderer, renderer.Library);
        }

        [UnmanagedCallersOnly]
        private unsafe static void CleanUp(Allocation system)
        {
            ref VulkanRendererSystem renderer = ref system.Read<VulkanRendererSystem>();
            renderer.Dispose();
            system.Dispose();
        }

        [UnmanagedCallersOnly]
        private unsafe static uint BeginRender(Allocation system, Vector4 clearColor)
        {
            ref VulkanRendererSystem renderer = ref system.Read<VulkanRendererSystem>();
            return renderer.BeginRender(clearColor) ? (uint)0 : 1;
        }

        [UnmanagedCallersOnly]
        private unsafe static void Render(RenderFunction.Input input)
        {
            ref VulkanRendererSystem renderer = ref input.system.Read<VulkanRendererSystem>();
            renderer.Render(input.Renderers, input.material, input.shader, input.mesh);
        }

        [UnmanagedCallersOnly]
        private unsafe static uint EndRender(Allocation system)
        {
            ref VulkanRendererSystem renderer = ref system.Read<VulkanRendererSystem>();
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
            ref VulkanRendererSystem renderer = ref allocation.Read<VulkanRendererSystem>();
            renderer.SurfaceCreated(surface);
        }
    }
}