using Materials;
using Simulation;
using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledImage : IDisposable
    {
        public readonly eint materialEntity;
        public readonly uint textureVersion;
        public readonly MaterialTextureBinding binding;
        public readonly Image image;
        public readonly ImageView imageView;
        public readonly DeviceMemory imageMemory;
        public readonly Sampler sampler;

        public CompiledImage(eint materialEntity, uint textureVersion, MaterialTextureBinding binding, Image image, ImageView imageView, DeviceMemory imageMemory, Sampler sampler)
        {
            this.materialEntity = materialEntity;
            this.textureVersion = textureVersion;
            this.binding = binding;
            this.image = image;
            this.imageView = imageView;
            this.imageMemory = imageMemory;
            this.sampler = sampler;
        }

        public readonly void Dispose()
        {
            sampler.Dispose();
            imageView.Dispose();
            image.Dispose();
            imageMemory.Dispose();
        }
    }
}