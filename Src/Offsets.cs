// commented offsets are universal offsets

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
          public static readonly int DebugDisableTimeoutDisconnect = 0x6542420;
          public static readonly int EnableLoadModule = 0x6531780;
          public static readonly int PartyPlayerInactivityTimeoutInSeconds = 0x64f7d90;
          public static readonly int TaskSchedulerTargetFps = 0x6e54ccc;
          public static readonly int WebSocketServiceEnableClientCreation = 0x654F028;
     }
    
    public static class FakeDataModel {
         public static readonly int Pointer = 0x76b46b8;
    }

    public static class Instance {
         public static readonly int ChildrenStart = 0x68;
         public static readonly int Name = 0xa8;
         public static readonly int LocalPlayer = 0x128;
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