using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Obj2x.UI;

// OpenGL 함수 (Avalonia가 준 GetProcAddress로 읽는다). Windows는 ANGLE(OpenGL ES 3), 맥은 OpenGL 3.2 core.
// 두 쪽에 다 있는 함수만 쓴다.
public sealed unsafe class GlApi {
    public const int DEPTH_TEST = 0x0B71, CULL_FACE = 0x0B44, BLEND = 0x0BE2, BACK = 0x0405, CCW = 0x0901, LEQUAL = 0x0203, LESS = 0x0201;
    public const int COLOR_BUFFER_BIT = 0x4000, DEPTH_BUFFER_BIT = 0x0100;
    public const int SRC_ALPHA = 0x0302, ONE_MINUS_SRC_ALPHA = 0x0303, ONE = 1;
    public const int ARRAY_BUFFER = 0x8892, ELEMENT_ARRAY_BUFFER = 0x8893, STATIC_DRAW = 0x88E4;
    public const int FLOAT = 0x1406, UNSIGNED_INT = 0x1405, UNSIGNED_BYTE = 0x1401, TRIANGLES = 4;
    public const int VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30, COMPILE_STATUS = 0x8B81, LINK_STATUS = 0x8B82;
    public const int TEXTURE_2D = 0x0DE1, TEXTURE0 = 0x84C0, RGBA = 0x1908, RGBA8 = 0x8058;
    public const int TEXTURE_MIN_FILTER = 0x2801, TEXTURE_MAG_FILTER = 0x2800, TEXTURE_WRAP_S = 0x2802, TEXTURE_WRAP_T = 0x2803;
    public const int LINEAR = 0x2601, LINEAR_MIPMAP_LINEAR = 0x2703, REPEAT = 0x2901, CLAMP_TO_EDGE = 0x812F, UNPACK_ALIGNMENT = 0x0CF5, PACK_ALIGNMENT = 0x0D05;
    public const int FRAMEBUFFER = 0x8D40, READ_FRAMEBUFFER = 0x8CA8, DRAW_FRAMEBUFFER = 0x8CA9, RENDERBUFFER = 0x8D41;
    public const int COLOR_ATTACHMENT0 = 0x8CE0, DEPTH_ATTACHMENT = 0x8D00, DEPTH_COMPONENT24 = 0x81A6, FRAMEBUFFER_COMPLETE = 0x8CD5, NEAREST = 0x2600;
    public const int VENDOR = 0x1F00, RENDERER = 0x1F01, VERSION = 0x1F02, MAX_SAMPLES = 0x8D57;

    public readonly delegate* unmanaged<int, void> Enable, Disable, Clear, DepthFunc, CullFace, FrontFace, ActiveTexture, UseProgram, EnableVertexAttribArray, BindVertexArray, GenerateMipmap;
    public readonly delegate* unmanaged<float, float, float, float, void> ClearColor;
    public readonly delegate* unmanaged<int, int, int, int, void> Viewport;
    public readonly delegate* unmanaged<byte, void> DepthMask;
    public readonly delegate* unmanaged<int, int, void> BlendFunc, BindBuffer, BindTexture, AttachShader, PixelStorei, BindFramebuffer, BindRenderbuffer;
    public readonly delegate* unmanaged<int, int*, void> GenBuffers, DeleteBuffers, GenVertexArrays, DeleteVertexArrays, GenTextures, DeleteTextures, GenFramebuffers, DeleteFramebuffers, GenRenderbuffers, DeleteRenderbuffers, GetIntegerv;
    public readonly delegate* unmanaged<int, IntPtr, void*, int, void> BufferData;
    public readonly delegate* unmanaged<int, int, int, byte, int, IntPtr, void> VertexAttribPointer;
    public readonly delegate* unmanaged<int, int> CreateShader, CheckFramebufferStatus;
    public readonly delegate* unmanaged<int> CreateProgram, GetError;
    public readonly delegate* unmanaged<int, int, byte**, int*, void> ShaderSource;
    public readonly delegate* unmanaged<int, void> CompileShader, LinkProgram, DeleteShader, DeleteProgram;
    public readonly delegate* unmanaged<int, int, int*, void> GetShaderiv, GetProgramiv;
    public readonly delegate* unmanaged<int, int, int*, byte*, void> GetShaderInfoLog, GetProgramInfoLog;
    public readonly delegate* unmanaged<int, int, byte*, void> BindAttribLocation;
    public readonly delegate* unmanaged<int, byte*, int> GetUniformLocation;
    public readonly delegate* unmanaged<int, int, byte, float*, void> UniformMatrix4fv;
    public readonly delegate* unmanaged<int, float, float, float, float, void> Uniform4f;
    public readonly delegate* unmanaged<int, float, float, float, void> Uniform3f;
    public readonly delegate* unmanaged<int, int, void> Uniform1i;
    public readonly delegate* unmanaged<int, int, int, int, int, int, int, int, void*, void> TexImage2D;
    public readonly delegate* unmanaged<int, int, int, void> TexParameteri;
    public readonly delegate* unmanaged<int, int, int, IntPtr, void> DrawElements;
    public readonly delegate* unmanaged<int, IntPtr> GetString;
    public readonly delegate* unmanaged<int, int, int, int, int, int, void*, void> ReadPixels;
    public readonly delegate* unmanaged<int, int, int, int, int, void> RenderbufferStorageMultisample;
    public readonly delegate* unmanaged<int, int, int, int, void> FramebufferRenderbuffer;
    public readonly delegate* unmanaged<int, int, int, int, int, int, int, int, int, int, void> BlitFramebuffer;

    public readonly bool Es;          // OpenGL ES (ANGLE)
    public readonly int Major, Minor;

    public GlApi(Func<string, IntPtr> gpa, bool es, int major, int minor) {
        Es = es; Major = major; Minor = minor;
        IntPtr P(string n) { IntPtr p = gpa(n); if (p == IntPtr.Zero) p = gpa(n + "OES"); if (p == IntPtr.Zero) p = gpa(n + "EXT"); if (p == IntPtr.Zero) p = gpa(n + "ARB"); return p; }
        Enable = (delegate* unmanaged<int, void>)P("glEnable"); Disable = (delegate* unmanaged<int, void>)P("glDisable");
        Clear = (delegate* unmanaged<int, void>)P("glClear"); DepthFunc = (delegate* unmanaged<int, void>)P("glDepthFunc");
        CullFace = (delegate* unmanaged<int, void>)P("glCullFace"); FrontFace = (delegate* unmanaged<int, void>)P("glFrontFace");
        ActiveTexture = (delegate* unmanaged<int, void>)P("glActiveTexture"); UseProgram = (delegate* unmanaged<int, void>)P("glUseProgram");
        EnableVertexAttribArray = (delegate* unmanaged<int, void>)P("glEnableVertexAttribArray");
        BindVertexArray = (delegate* unmanaged<int, void>)P("glBindVertexArray"); GenerateMipmap = (delegate* unmanaged<int, void>)P("glGenerateMipmap");
        ClearColor = (delegate* unmanaged<float, float, float, float, void>)P("glClearColor");
        Viewport = (delegate* unmanaged<int, int, int, int, void>)P("glViewport");
        DepthMask = (delegate* unmanaged<byte, void>)P("glDepthMask");
        BlendFunc = (delegate* unmanaged<int, int, void>)P("glBlendFunc"); BindBuffer = (delegate* unmanaged<int, int, void>)P("glBindBuffer");
        BindTexture = (delegate* unmanaged<int, int, void>)P("glBindTexture"); AttachShader = (delegate* unmanaged<int, int, void>)P("glAttachShader");
        PixelStorei = (delegate* unmanaged<int, int, void>)P("glPixelStorei");
        BindFramebuffer = (delegate* unmanaged<int, int, void>)P("glBindFramebuffer"); BindRenderbuffer = (delegate* unmanaged<int, int, void>)P("glBindRenderbuffer");
        GenBuffers = (delegate* unmanaged<int, int*, void>)P("glGenBuffers"); DeleteBuffers = (delegate* unmanaged<int, int*, void>)P("glDeleteBuffers");
        GenVertexArrays = (delegate* unmanaged<int, int*, void>)P("glGenVertexArrays"); DeleteVertexArrays = (delegate* unmanaged<int, int*, void>)P("glDeleteVertexArrays");
        GenTextures = (delegate* unmanaged<int, int*, void>)P("glGenTextures"); DeleteTextures = (delegate* unmanaged<int, int*, void>)P("glDeleteTextures");
        GenFramebuffers = (delegate* unmanaged<int, int*, void>)P("glGenFramebuffers"); DeleteFramebuffers = (delegate* unmanaged<int, int*, void>)P("glDeleteFramebuffers");
        GenRenderbuffers = (delegate* unmanaged<int, int*, void>)P("glGenRenderbuffers"); DeleteRenderbuffers = (delegate* unmanaged<int, int*, void>)P("glDeleteRenderbuffers");
        GetIntegerv = (delegate* unmanaged<int, int*, void>)P("glGetIntegerv");
        BufferData = (delegate* unmanaged<int, IntPtr, void*, int, void>)P("glBufferData");
        VertexAttribPointer = (delegate* unmanaged<int, int, int, byte, int, IntPtr, void>)P("glVertexAttribPointer");
        CreateShader = (delegate* unmanaged<int, int>)P("glCreateShader"); CheckFramebufferStatus = (delegate* unmanaged<int, int>)P("glCheckFramebufferStatus");
        CreateProgram = (delegate* unmanaged<int>)P("glCreateProgram"); GetError = (delegate* unmanaged<int>)P("glGetError");
        ShaderSource = (delegate* unmanaged<int, int, byte**, int*, void>)P("glShaderSource");
        CompileShader = (delegate* unmanaged<int, void>)P("glCompileShader"); LinkProgram = (delegate* unmanaged<int, void>)P("glLinkProgram");
        DeleteShader = (delegate* unmanaged<int, void>)P("glDeleteShader"); DeleteProgram = (delegate* unmanaged<int, void>)P("glDeleteProgram");
        GetShaderiv = (delegate* unmanaged<int, int, int*, void>)P("glGetShaderiv"); GetProgramiv = (delegate* unmanaged<int, int, int*, void>)P("glGetProgramiv");
        GetShaderInfoLog = (delegate* unmanaged<int, int, int*, byte*, void>)P("glGetShaderInfoLog"); GetProgramInfoLog = (delegate* unmanaged<int, int, int*, byte*, void>)P("glGetProgramInfoLog");
        BindAttribLocation = (delegate* unmanaged<int, int, byte*, void>)P("glBindAttribLocation");
        GetUniformLocation = (delegate* unmanaged<int, byte*, int>)P("glGetUniformLocation");
        UniformMatrix4fv = (delegate* unmanaged<int, int, byte, float*, void>)P("glUniformMatrix4fv");
        Uniform4f = (delegate* unmanaged<int, float, float, float, float, void>)P("glUniform4f");
        Uniform3f = (delegate* unmanaged<int, float, float, float, void>)P("glUniform3f");
        Uniform1i = (delegate* unmanaged<int, int, void>)P("glUniform1i");
        TexImage2D = (delegate* unmanaged<int, int, int, int, int, int, int, int, void*, void>)P("glTexImage2D");
        TexParameteri = (delegate* unmanaged<int, int, int, void>)P("glTexParameteri");
        DrawElements = (delegate* unmanaged<int, int, int, IntPtr, void>)P("glDrawElements");
        GetString = (delegate* unmanaged<int, IntPtr>)P("glGetString");
        ReadPixels = (delegate* unmanaged<int, int, int, int, int, int, void*, void>)P("glReadPixels");
        RenderbufferStorageMultisample = (delegate* unmanaged<int, int, int, int, int, void>)P("glRenderbufferStorageMultisample");
        FramebufferRenderbuffer = (delegate* unmanaged<int, int, int, int, void>)P("glFramebufferRenderbuffer");
        BlitFramebuffer = (delegate* unmanaged<int, int, int, int, int, int, int, int, int, int, void>)P("glBlitFramebuffer");
    }

    public bool HasVao => BindVertexArray != null && GenVertexArrays != null;
    public bool HasMsaa => RenderbufferStorageMultisample != null && BlitFramebuffer != null && (Es ? Major >= 3 : Major >= 3);

    public string Str(int name) { IntPtr p = GetString(name); return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p); }

    public int Gen(delegate* unmanaged<int, int*, void> f) { int id; f(1, &id); return id; }
    public void Del(delegate* unmanaged<int, int*, void> f, int id) { if (id != 0) f(1, &id); }

    public int Uniform(int prog, string name) {
        byte[] b = Encoding.ASCII.GetBytes(name + "\0");
        fixed (byte* p = b) return GetUniformLocation(prog, p);
    }
    public void Attrib(int prog, int idx, string name) {
        byte[] b = Encoding.ASCII.GetBytes(name + "\0");
        fixed (byte* p = b) BindAttribLocation(prog, idx, p);
    }

    public int Shader(int type, string src) {
        int s = CreateShader(type);
        byte[] b = Encoding.ASCII.GetBytes(src);
        fixed (byte* p = b) { byte* pp = p; int len = b.Length; ShaderSource(s, 1, &pp, &len); }
        CompileShader(s);
        int ok; GetShaderiv(s, COMPILE_STATUS, &ok);
        if (ok == 0) {
            byte* log = stackalloc byte[4096]; int n;
            GetShaderInfoLog(s, 4096, &n, log);
            throw new Exception("GL shader: " + Marshal.PtrToStringAnsi((IntPtr)log, n));
        }
        return s;
    }
    public int Program(string vs, string fs, params string[] attribs) {
        int v = Shader(VERTEX_SHADER, vs), f = Shader(FRAGMENT_SHADER, fs), p = CreateProgram();
        AttachShader(p, v); AttachShader(p, f);
        for (int i = 0; i < attribs.Length; i++) Attrib(p, i, attribs[i]);
        LinkProgram(p);
        int ok; GetProgramiv(p, LINK_STATUS, &ok);
        if (ok == 0) {
            byte* log = stackalloc byte[4096]; int n;
            GetProgramInfoLog(p, 4096, &n, log);
            throw new Exception("GL program: " + Marshal.PtrToStringAnsi((IntPtr)log, n));
        }
        DeleteShader(v); DeleteShader(f);
        return p;
    }
    public void Mat4(int loc, float[] m) { fixed (float* p = m) UniformMatrix4fv(loc, 1, 0, p); }
}
