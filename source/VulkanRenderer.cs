using Simulation;
using System;
using System.Collections.Generic;
using Unmanaged;
using Vulkan;

namespace Rendering.Systems
{
    public class VulkanRenderer : IRenderSystem, IDisposable
    {
        private Library library;
        private readonly Dictionary<eint, Instance> instances;

        static FixedString IRenderSystem.Label => "vulkan";

        public VulkanRenderer()
        {
            library = new();
            instances = new();
        }

        public void Dispose()
        {
            foreach (Instance instance in instances.Values)
            {
                instance.Dispose();
            }

            instances.Clear();
            library.Dispose();
        }

        void IRenderSystem.Initialize(eint entity, ReadOnlySpan<FixedString> extensionNames)
        {
            Instance instance = new("Game", "Engine", extensionNames.ToArray());
            instances.Add(entity, instance);
        }
    }
}