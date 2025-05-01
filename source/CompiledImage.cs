using Materials.Components;
using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledImage : IDisposable
    {
        public readonly uint materialEntity;
        public readonly ushort textureVersion;
        public readonly TextureBinding binding;
        public readonly Image image;
        public readonly ImageView imageView;
        public readonly DeviceMemory imageMemory;
        public readonly Sampler sampler;

        public CompiledImage(uint materialEntity, ushort textureVersion, TextureBinding binding, Image image, ImageView imageView, DeviceMemory imageMemory, Sampler sampler)
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