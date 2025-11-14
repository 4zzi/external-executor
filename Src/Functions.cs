using System.Linq;
using Microsoft.Win32;
using RMemory;
using Main;
using System.Text;

namespace Functions
{
    public class RobloxInstance
    {
        public IntPtr Self;

        public RobloxInstance(IntPtr addy)
        {
            Self = addy;
        }

        public static implicit operator bool(RobloxInstance obj) // can act like true if the address is valid
        {
            return obj != null && obj.Self != IntPtr.Zero;
        }

        public string GetName(ulong addr)
        {
            ulong namePtr = Memory.Read<ulong>((ulong)Self + (ulong)Offsets.Instance.Name);

            // If string length > 15, the actual string is stored elsewhere
            int length = Memory.Read<int>(namePtr + 0x10);
            if (length > 15)
                namePtr = Memory.Read<ulong>(namePtr);

            // Read up to 640 bytes or until null terminator
            string result = string.Empty;
            for (int i = 0; i < 640; i++)
            {
                byte b = Memory.Read<byte>(namePtr + (ulong)i);
                if (b == 0)
                    break;

                result += (char)b;
            }

            return result;
        }

        public string Name
        {
            get
            {
                ulong namePtr = Memory.Read<ulong>((ulong)Self + (ulong)Offsets.Instance.Name);
                int length = Memory.Read<int>(namePtr + 0x10);

                if (length <= 0 || length > 128) return "";

                ulong dataPtr = (length >= 16) ? Memory.Read<ulong>(namePtr) : namePtr;

                char[] chars = new char[length];
                for (int i = 0; i < length; i++)
                    chars[i] = (char)Memory.Read<byte>(dataPtr + (ulong)i);

                string result = new string(chars);
                return string.IsNullOrWhiteSpace(result) ? "" : result;
            }
        }

        public string ClassName
        {
            get
            {
                ulong classDesc = Memory.Read<ulong>((ulong)Self + 0x18);
                ulong namePtr = Memory.Read<ulong>(classDesc + 0x8);
                int length = Memory.Read<int>(namePtr + 0x10);
                ulong dataPtr = (length > 16) ? Memory.Read<ulong>(namePtr) : namePtr;

                char[] chars = new char[length];
                for (int i = 0; i < length; i++)
                {
                    chars[i] = (char)Memory.Read<byte>(dataPtr + (ulong)i);
                }

                return new string(chars);
            }
        }

        public string GetFullName()
        {
            var names = new List<string>();
            RobloxInstance current = this;

            while (current != null && current.Self != IntPtr.Zero)
            {
                names.Add(current.Name);
                current = current.Parent();
            }

            names.Reverse(); // because we collected from child -> parent
            return string.Join(".", names);
        }

        public void UnlockModule()
        {
            if (ClassName != "ModuleScript")
                throw new Exception("[*] Instance::UnlockModule(): " + Name + " is not a ModuleScript");

            bool ok =
                Memory.WriteTo(this, (ulong)Offsets.ModuleScript.Flags, 0x100000000) &&
                Memory.WriteTo(this, (ulong)Offsets.ModuleScript.IsCoreScript, 0x1);
            if (!ok)
                throw new Exception("[*] Instance::UnlockModule(): failed to unlock module " + Name);
            else
            {
            }
        }

        public void SpoofWith(IntPtr instancePtr)
        {
            ulong value = (ulong)instancePtr;
            bool ok = Memory.Write<ulong>((ulong)Self + 0x8UL, value);
            if (!ok)
                throw new Exception($"[*] SpoofWith(): failed to spoof instance {Name} with {value}");
        }

        //

        public RobloxInstance Parent()
        {
            return new RobloxInstance(Memory.Read<IntPtr>((ulong)Self + 0x60));
        }

        public List<RobloxInstance> GetDescendants()
        {
            var descendants = new List<RobloxInstance>();
            var queue = new Queue<RobloxInstance>(GetChildren());

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node && node.Self != IntPtr.Zero)
                {
                    descendants.Add(node);
                    var kids = node.GetChildren();
                    if (kids != null)
                    {
                        foreach (var k in kids)
                            if (k && k.Self != IntPtr.Zero)
                                queue.Enqueue(k);
                    }
                }
            }

            return descendants;
        }

        public List<RobloxInstance> GetChildren()
        {
            var children = new List<RobloxInstance>();

            ulong start = Memory.Read<ulong>((ulong)Self + (ulong)Offsets.Instance.ChildrenStart);
            ulong end = Memory.Read<ulong>(start + 0x8);
            ulong ptr = Memory.Read<ulong>(start);

            for (; ptr < end; ptr += 0x10UL)
            {
                ulong childSelf = Memory.Read<ulong>(ptr);
                if (childSelf != 0)
                    children.Add(new RobloxInstance((IntPtr)childSelf));
            }

            return children;
        }

        public RobloxInstance FindFirstChild(string name)
        {
            foreach (var child in GetChildren())
            {
                if (child.Name == name)
                    return child;
            }

            return new RobloxInstance(IntPtr.Zero);
        }

        public RobloxInstance GetService(string name)
        {
            if (Roblox.Game.FindFirstChild(name))
            {
                return Roblox.Game.FindFirstChild(name);
            }

            return new RobloxInstance(IntPtr.Zero);
        }

        public RobloxInstance WaitForChild(string name, int timeoutMs = 5000)
        {
            var start = Environment.TickCount;
            RobloxInstance child;
            do
            {
                child = FindFirstChild(name);
                if (child != null && child.Self != IntPtr.Zero)
                    return child;

                Thread.Sleep(10); // tiny delay to avoid busy wait
            }
            while (Environment.TickCount - start < timeoutMs);

            return new RobloxInstance(IntPtr.Zero);
        }

        public RobloxInstance FindFirstChildOfClass(string className)
        {
            try
            {
                var queue = new Queue<RobloxInstance>(GetChildren());
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    if (node && node.Name == className)
                        return node;

                    var kids = node.GetChildren();
                    if (kids != null)
                    {
                        foreach (var k in kids)
                            if (k)
                                queue.Enqueue(k);
                    }
                }
            }
            catch { }

            return new RobloxInstance(IntPtr.Zero);
        }

        public RobloxInstance FindFirstChildFromPath(string path)
        {
            RobloxInstance last = null;
            var tokens = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (last == null)
                    last = FindFirstChild(token);
                else
                    last = last.FindFirstChild(token);

                if (last == null || last.Self == IntPtr.Zero)
                    return new RobloxInstance(IntPtr.Zero);
            }

            return last ?? new RobloxInstance(IntPtr.Zero);
        }
    }
}