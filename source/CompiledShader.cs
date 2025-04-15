using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledShader : IDisposable, IEquatable<CompiledShader>
    {
        public readonly bool isInstanced;
        public readonly ushort vertexVersion;
        public readonly ushort fragmentVersion;
        public readonly ShaderModule vertexShader;
        public readonly ShaderModule fragmentShader;

        public CompiledShader(ushort vertexVersion, ushort fragmentVersion, ShaderModule vertexShader, ShaderModule fragmentShader, bool isInstanced)
        {
            this.isInstanced = isInstanced;
            this.vertexVersion = vertexVersion;
            this.fragmentVersion = fragmentVersion;
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
            return vertexVersion == other.vertexVersion && fragmentVersion == other.fragmentVersion && vertexShader.Equals(other.vertexShader) && fragmentShader.Equals(other.fragmentShader);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(vertexVersion, vertexShader, fragmentShader);
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