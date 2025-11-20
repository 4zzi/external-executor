// commented offsets are universal offsets

using System.Reflection.Metadata;
using Functions;
using Main;
using RMemory;

namespace Offsets
{
     /*
      bytecode pointer 0x10
      bytecode size 0x20
      children end + classname 0x8
      fake data model to datamodel 0x1C0
      
      local script
          pointer 0x10
      module script
          pointer 0x10
          stringlength 0x10
          flags (iscorescript - 0x4)
     */

     public static class FFlags
     {
          public static readonly int TaskSchedulerTargetFps = 0x6e54ccc;
          public static readonly int WebSocketServiceEnableClientCreation = 0x654F028;
     }
    
    public static class FakeDataModel {
         public static readonly int VisualEnginePointer = 0x743BDD0;
         public static readonly int VisualEngineToDataModel1 = 0x700;
         public static readonly int VisualEngineToDataModel2 = 0x1C0;
         public static readonly int ScriptContext = 0x3F0;
    }

    public static class Instance {
         public static readonly int ChildrenStart = 0x68;
         public static readonly int Name = 0xa8;
         public static readonly int LocalPlayer = 0x128;
         public static readonly int ClassDescriptor = 0x18;
         public static readonly int ClassName = 0x8;
    }

    public static class LocalScript {
         public static readonly int ByteCode = 0x1A0;
         public static readonly int Hash = 0x1B0;
    }

    public static class ModuleScript {
         public static readonly int ByteCode = 0x148;
         public static readonly int Hash = 0x160;
         public static readonly int IsCoreScript = 0x914;
         public static readonly int Flags = 0x17c;
    }
}