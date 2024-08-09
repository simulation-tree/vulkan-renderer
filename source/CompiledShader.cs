using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledShader : IDisposable, IEquatable<CompiledShader>
    {
        public readonly uint version;
        public readonly ShaderModule vertexShader;
        public readonly ShaderModule fragmentShader;

        public CompiledShader(uint version, ShaderModule vertexShader, ShaderModule fragmentShader)
        {
            this.version = version;
            this.vertexShader = vertexShader;
            this.fragmentShader = fragmentShader;
        }

        public readonly void Dispose()
        {
            vertexShader.Dispose();
            fragmentShader.Dispose();
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is CompiledShader shader && Equals(shader);
        }

        public readonly bool Equals(CompiledShader other)
        {
            return version == other.version && vertexShader.Equals(other.vertexShader) && fragmentShader.Equals(other.fragmentShader);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(version, vertexShader, fragmentShader);
        }

        public static bool operator ==(CompiledShader left, CompiledShader right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CompiledShader left, CompiledShader right)
        {
            return !(left == right);
        }
    }
}