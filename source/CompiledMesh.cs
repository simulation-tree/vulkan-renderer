using Collections.Generic;
using Shaders;
using System;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    /// <summary>
    /// A mesh object built from an existing mesh.
    /// </summary>
    public readonly struct CompiledMesh : IDisposable
    {
        public readonly uint version;
        public readonly uint indexCount;
        public readonly VertexBuffer vertexBuffer;
        public readonly IndexBuffer indexBuffer;

        private readonly Array<ShaderVertexInputAttribute> attributeLayout;

        public readonly USpan<ShaderVertexInputAttribute> VertexAttributes => attributeLayout.AsSpan();
        public readonly bool IsDisposed => attributeLayout.IsDisposed;

        public CompiledMesh(uint meshVersion, uint indexCount, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, USpan<ShaderVertexInputAttribute> attributeLayout)
        {
            this.version = meshVersion;
            this.indexCount = indexCount;
            this.vertexBuffer = vertexBuffer;
            this.indexBuffer = indexBuffer;
            this.attributeLayout = new(attributeLayout);
        }

        public readonly void Dispose()
        {
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
            attributeLayout.Dispose();
        }
    }
}