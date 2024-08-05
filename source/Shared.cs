using Unmanaged;
using Vulkan;

namespace Rendering.Systems
{
    public static class Shared
    {
        public static readonly Library library;

        static Shared()
        {
            library = new();
            Allocations.Finish += () =>
            {
                library.Dispose();
            };
        }
    }
}