using Vulkan;

namespace Rendering.Systems
{
    public static class Shared
    {
        private static readonly Library library;
        private static int count;

        static Shared()
        {
            library = new();
        }

        public static Library TakeLibrary()
        {
            count++;
            return library;
        }

        public static void ReturnLibrary()
        {
#if DEBUG
            if (count == 0)
            {
                throw new System.Exception("Library can't exist to return");
            }
#endif

            count--;
            if (count == 0)
            {
                library.Dispose();
            }
        }
    }
}