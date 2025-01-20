using Collections;
using System;
using System.Collections.Generic;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct Library : IDisposable, IEquatable<Library>
    {
        public readonly VkVersion version;

        private readonly Text name;

        public readonly bool IsDisposed => name.IsDisposed;
        public readonly USpan<char> Name => name.AsSpan();

        /// <summary>
        /// Initializes the vulkan library.
        /// </summary>
        public Library() : this(default)
        {
        }

        /// <summary>
        /// Initializes the vulkan library.
        /// </summary>
        public Library(USpan<char> libraryName)
        {
            name = new(libraryName);
            VkResult result = vkInitialize(libraryName.Length == 0 ? null : libraryName.ToString());
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

        public readonly Instance CreateInstance(USpan<char> applicationName, USpan<char> engineName, IEnumerable<FixedString>? extensions = null)
        {
            using Collections.List<FixedString> extensionNames = new();
            if (extensions != null)
            {
                foreach (FixedString extension in extensions)
                {
                    extensionNames.Add(extension);
                }
            }

            return new(this, applicationName, engineName, extensionNames.AsSpan());
        }

        public readonly Instance CreateInstance(USpan<char> applicationName, USpan<char> engineName, USpan<FixedString> extensions)
        {
            return new(this, applicationName, engineName, extensions);
        }

        public readonly Instance CreateInstance(string applicationName, string engineName, IEnumerable<FixedString>? extensions = null)
        {
            return CreateInstance(applicationName.AsUSpan(), engineName.AsUSpan(), extensions);
        }

        public readonly Instance CreateInstance(string applicationName, string engineName, USpan<FixedString> extensions)
        {
            return CreateInstance(applicationName.AsUSpan(), engineName.AsUSpan(), extensions);
        }

        /// <summary>
        /// Retrieves a new list containing names of all available global layers.
        /// </summary>
        public readonly Array<FixedString> GetGlobalLayers()
        {
            uint count = 0;
            VkResult result = vkEnumerateInstanceLayerProperties(&count, null);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate instance layer properties: {result}");
            }

            if (count > 0)
            {
                VkLayerProperties* properties = stackalloc VkLayerProperties[(int)count];
                result = vkEnumerateInstanceLayerProperties(&count, properties);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to enumerate instance layer properties: {result}");
                }

                Array<FixedString> availableInstanceLayers = new(count);
                for (uint i = 0; i < count; i++)
                {
                    availableInstanceLayers[i] = new(properties[i].layerName);
                }

                return availableInstanceLayers;
            }
            else
            {
                return new();
            }
        }

        /// <summary>
        /// Retrieves a new list containing names of all available global extensions.
        /// </summary>
        public readonly Array<FixedString> GetGlobalExtensions()
        {
            uint count = 0;
            VkResult result = vkEnumerateInstanceExtensionProperties(&count, null);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate instance extension properties: {result}");
            }

            if (count > 0)
            {
                VkExtensionProperties* extensionProperties = stackalloc VkExtensionProperties[(int)count];
                result = vkEnumerateInstanceExtensionProperties(&count, extensionProperties);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to enumerate instance extension properties: {result}");
                }

                Array<FixedString> availableInstanceExtensions = new(count);
                for (uint i = 0; i < count; i++)
                {
                    availableInstanceExtensions[i] = new(extensionProperties[i].extensionName);
                }

                return availableInstanceExtensions;
            }
            else
            {
                return new();
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Library library && Equals(library);
        }

        public readonly bool Equals(Library other)
        {
            return version.Equals(other.version) && name.Equals(other.name);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(version, name);
        }

        public static bool operator ==(Library left, Library right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Library left, Library right)
        {
            return !(left == right);
        }
    }
}
