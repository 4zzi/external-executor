// commented offsets are universal offsets

namespace Offsets
{
     /*
      bytecode pointer 0x10
      bytecode size 0x20
      children end + classname 0x8
      
     local script
          bytecode 0x1a8
          hash 0x1b8
      module script
          bytecode 0x150
          hash 0x168
          require bypass 0x8e0
          stringlength 0x10
          iscorescript 0x188
          flags (0x188 - 0x4) or in short 0x184
     */

     public static class FFlags
     {
          public static readonly int DebugDisableTimeoutDisconnect = 0x6437ef8;
          public static readonly int EnableLoadModule = 0x6427258;
          public static readonly int PartyPlayerInactivityTimeoutInSeconds = 0x63edd74;
          public static readonly int TaskSchedulerTargetFps = 0x6d41110;
          public static readonly int WebSocketServiceEnableClientCreation = 0x64449E0;
     }
    
    public static class FakeDataModel {
         public static readonly int Pointer = 0x759fd28;
         public static readonly int Real = 0x1c0;
    }

    public static class Instance {
         public static readonly int ChildrenStart = 0x70;
         public static readonly int Name = 0x90;
    }

    public static class LocalScript {
         public static readonly int Embedded = 0x1c0; 
    }

    public static class ModuleScript {
         public static readonly int Embedded = 0x160;
         public static readonly int RunContext = 0x150;
         public static readonly int ScriptContext = 0x3e0;
    }
}