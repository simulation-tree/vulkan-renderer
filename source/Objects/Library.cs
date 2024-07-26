using System;
using Unmanaged.Collections;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct Library : IDisposable
    {
        public readonly VkVersion version;

        private readonly UnmanagedArray<char> name;

        public readonly bool IsDisposed => name.IsDisposed;
        public readonly ReadOnlySpan<char> Name => name.AsSpan();

        public Library() : this(default)
        {
        }

        public Library(ReadOnlySpan<char> libraryName)
        {
            name = new(libraryName);
            VkResult result = vkInitialize(libraryName == default ? null : libraryName.ToString());
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to initialize graphics library: {result}");
            }

            version = vkEnumerateInstanceVersion();
            if (version < VkVersion.Version_1_1)
            {
                throw new PlatformNotSupportedException("Vulkan 1.1 or above is required");
            }
        }

        public readonly void Dispose()
        {
            name.Dispose();
        }
    }
}
