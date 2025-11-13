using System;
using System.Collections.Generic;

namespace Luau
{
    public class BytecodeEncoder
    {
        public virtual void Encode(uint[] data, int count)
        {
            // do nothing
        }
    }

    public class BytecodeBuilder
    {
        public struct StringRef
        {
            public string Data;
            public int Length;
            public static bool operator ==(StringRef a, StringRef b) => a.Data == b.Data && a.Length == b.Length;
            public static bool operator !=(StringRef a, StringRef b) => !(a == b);
            public override bool Equals(object obj) => obj is StringRef other && this == other;
            public override int GetHashCode() => (Data?.GetHashCode() ?? 0) ^ Length.GetHashCode();
        }

        public struct TableShape
        {
            public const int kMaxLength = 32;
            public int[] Keys;
            public uint Length;
            public static bool operator ==(TableShape a, TableShape b)
            {
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a.Keys[i] != b.Keys[i]) return false;
                return true;
            }
            public static bool operator !=(TableShape a, TableShape b) => !(a == b);
            public override bool Equals(object obj) => obj is TableShape other && this == other;
            public override int GetHashCode()
            {
                int hash = 17;
                for (int i = 0; i < Length; i++)
                    hash = hash * 31 + Keys[i];
                return hash;
            }
        }

        public enum DumpFlags
        {
            Dump_Code = 1 << 0,
            Dump_Lines = 1 << 1,
            Dump_Source = 1 << 2,
            Dump_Locals = 1 << 3,
            Dump_Remarks = 1 << 4,
            Dump_Types = 1 << 5,
        }

        public enum LuauOpcode
        {
            LOP_NOP,
            LOP_BREAK,
            LOP_LOADNIL,
            LOP_LOADB,
            LOP_LOADN,
            LOP_LOADK,
            LOP_MOVE,
            LOP_GETGLOBAL,
            LOP_SETGLOBAL,
            LOP_GETUPVAL,
            LOP_SETUPVAL,
            LOP_CLOSEUPVALS,
            LOP_GETIMPORT,
            LOP_GETTABLE,
            LOP_SETTABLE,
            LOP_GETTABLEKS,
            LOP_SETTABLEKS,
            LOP_GETTABLEN,
            LOP_SETTABLEN,
            LOP_NEWCLOSURE,
            LOP_NAMECALL,
            LOP_CALL,
            LOP_RETURN,
            LOP_JUMP,
            LOP_JUMPBACK,
            LOP_JUMPIF,
            LOP_JUMPIFNOT,
            LOP_JUMPIFEQ,
            LOP_JUMPIFLE,
            LOP_JUMPIFLT,
            LOP_JUMPIFNOTEQ,
            LOP_JUMPIFNOTLE,
            LOP_JUMPIFNOTLT,
            LOP_ADD,
            LOP_SUB,
            LOP_MUL,
            LOP_DIV,
            LOP_MOD,
            LOP_POW,
            LOP_ADDK,
            LOP_SUBK,
            LOP_MULK,
            LOP_DIVK,
            LOP_MODK,
            LOP_POWK,
            LOP_AND,
            LOP_OR,
            LOP_ANDK,
            LOP_ORK,
            LOP_CONCAT,
            LOP_NOT,
            LOP_MINUS,
            LOP_LENGTH,
            LOP_NEWTABLE,
            LOP_DUPTABLE,
            LOP_SETLIST,
            LOP_FORNPREP,
            LOP_FORNLOOP,
            LOP_FORGLOOP,
            LOP_FORGPREP_INEXT,
            LOP_FASTCALL3,
            LOP_FORGPREP_NEXT,
            LOP_NATIVECALL,
            LOP_GETVARARGS,
            LOP_DUPCLOSURE,
            LOP_PREPVARARGS,
            LOP_LOADKX,
            LOP_JUMPX,
            LOP_FASTCALL,
            LOP_COVERAGE,
            LOP_CAPTURE,
            LOP_SUBRK,
            LOP_DIVRK,
            LOP_FASTCALL1,
            LOP_FASTCALL2,
        }

        private class Constant
        {
            public enum Type
            {
                Type_Nil,
                Type_Boolean,
                Type_Number,
                Type_Vector,
                Type_String,
                Type_Import,
                Type_Table,
                Type_Closure
            }
            public Type ConstType;
            public bool ValueBoolean;
            public double ValueNumber;
            public float[] ValueVector = new float[4];
            public uint ValueString;
            public uint ValueImport;
            public uint ValueTable;
            public uint ValueClosure;
        }

        private class ConstantKey
        {
            public Constant.Type Type;
            public ulong Value;
            public ulong Extra;
            public static bool operator ==(ConstantKey a, ConstantKey b) => a.Type == b.Type && a.Value == b.Value && a.Extra == b.Extra;
            public static bool operator !=(ConstantKey a, ConstantKey b) => !(a == b);
            public override bool Equals(object obj) => obj is ConstantKey other && this == other;
            public override int GetHashCode() => Type.GetHashCode() ^ Value.GetHashCode() ^ Extra.GetHashCode();
        }

        private class Function
        {
            public string Data;
            public byte MaxStackSize;
            public byte NumParams;
            public byte NumUpvalues;
            public bool IsVararg;
            public uint DebugName;
            public int DebugLineDefined;
            public string Dump;
            public string DumpName;
            public List<int> DumpInstOffs = new();
            public string TypeInfo;
        }

        private class DebugLocal { public uint Name; public byte Reg; public uint StartPC; public uint EndPC; }
        private class DebugUpval { public uint Name; }
        private class TypedLocal { public LuauBytecodeType Type; public byte Reg; public uint StartPC; public uint EndPC; }
        private class TypedUpval { public LuauBytecodeType Type; }
        private class UserdataType { public string Name; public uint NameRef; public bool Used; }
        private class Jump { public uint Source; public uint Target; }

        private List<Function> functions = new();
        private uint currentFunction = uint.MaxValue;
        private uint mainFunction = uint.MaxValue;
        private List<uint> insns = new();
        private List<int> lines = new();
        private List<Constant> constants = new();
        private List<uint> protos = new();
        private List<Jump> jumps = new();
        private List<TableShape> tableShapes = new();
        private bool hasLongJumps = false;
        private BytecodeEncoder encoder;
        private string bytecode = string.Empty;
        private uint dumpFlags;
        private string tempTypeInfo = string.Empty;

        private List<DebugLocal> debugLocals = new();
        private List<DebugUpval> debugUpvals = new();
        private List<TypedLocal> typedLocals = new();
        private List<TypedUpval> typedUpvals = new();
        private List<UserdataType> userdataTypes = new();

        public BytecodeBuilder(BytecodeEncoder encoder = null) { this.encoder = encoder; }

        public uint BeginFunction(byte numparams, bool isvararg = false) => 0;
        public void EndFunction(byte maxstacksize, byte numupvalues, byte flags = 0) { }
        public void SetMainFunction(uint fid) { }
        public int AddConstantNil() => 0;
        public int AddConstantBoolean(bool value) => 0;
        public int AddConstantNumber(double value) => 0;
        public int AddConstantVector(float x, float y, float z, float w) => 0;
        public int AddConstantString(StringRef value) => 0;
        public int AddImport(uint iid) => 0;
        public int AddConstantTable(TableShape shape) => 0;
        public int AddConstantClosure(uint fid) => 0;
        public short AddChildFunction(uint fid) => 0;
        public void EmitABC(LuauOpcode op, byte a, byte b, byte c) { }
        public void EmitAD(LuauOpcode op, byte a, short d) { }
        public void EmitE(LuauOpcode op, int e) { }
        public void EmitAux(uint aux) { }
        public int EmitLabel() => 0;
        public bool PatchJumpD(int jumpLabel, int targetLabel) => false;
        public bool PatchSkipC(int jumpLabel, int targetLabel) => false;
        public void FoldJumps() { }
        public void ExpandJumps() { }
        public void SetFunctionTypeInfo(string value) { }
        public void PushLocalTypeInfo(LuauBytecodeType type, byte reg, uint startpc, uint endpc) { }
        public void PushUpvalTypeInfo(LuauBytecodeType type) { }
        public uint AddUserdataType(string name) => 0;
        public void UseUserdataType(uint index) { }
        public void SetDebugFunctionName(StringRef name) { }
        public void SetDebugFunctionLineDefined(int line) { }
        public void SetDebugLine(int line) { }
        public void PushDebugLocal(StringRef name, byte reg, uint startpc, uint endpc) { }
        public void PushDebugUpval(StringRef name) { }
        public int GetInstructionCount() => 0;
        public int GetTotalInstructionCount() => 0;
        public uint GetDebugPC() => 0;
        public void AddDebugRemark(string format, params object[] args) { }
        public void Finalize() { }
        public void SetDumpFlags(uint flags) => dumpFlags = flags;
        public void SetDumpSource(string source) { }
        public bool NeedsDebugRemarks() => (dumpFlags & (uint)DumpFlags.Dump_Remarks) != 0;
        public string GetBytecode() => bytecode;
        public string DumpFunction(uint id) => "";
        public string DumpEverything() => "";
        public string DumpSourceRemarks() => "";
        public string DumpTypeInfo() => "";
        public void AnnotateInstruction(ref string result, uint fid, uint instpos) { }
        public static uint GetImportId(int id0) => 0;
        public static uint GetImportId(int id0, int id1) => 0;
        public static uint GetImportId(int id0, int id1, int id2) => 0;
        public static int DecomposeImportId(uint ids, out int id0, out int id1, out int id2)
        {
            id0 = id1 = id2 = 0;
            return 0;
        }
        public static uint GetStringHash(StringRef key) => 0;
        public static string GetError(string message) => message;
        public static byte GetVersion() => 0;
        public static byte GetTypeEncodingVersion() => 0;
    }

    public enum LuauBytecodeType
    {
        Type_Nil,
        Type_Boolean,
        Type_Number,
        Type_Vector,
        Type_String,
        Type_Import,
        Type_Table,
        Type_Closure
    }
}
