using System;

namespace Rendering.Vulkan
{
    public readonly struct ShaderKey : IEquatable<ShaderKey>
    {
        public readonly uint vertexShader;
        public readonly uint fragmentShader;

        public ShaderKey(uint vertexShader, uint fragmentShader)
        {
            this.vertexShader = vertexShader;
            this.fragmentShader = fragmentShader;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is ShaderKey key && Equals(key);
        }

        public readonly bool Equals(ShaderKey other)
        {
            return vertexShader == other.vertexShader && fragmentShader == other.fragmentShader;
        }

        public readonly override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (int)vertexShader;
            hash = hash * 31 + (int)fragmentShader;
            return hash;
        }

        public static bool operator ==(ShaderKey left, ShaderKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ShaderKey left, ShaderKey right)
        {
            return !(left == right);
        }
    }
}