using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Permissions;

namespace Sovos.Infrastructure
{
  /// <summary>
  /// This class can convert any pointer to a managed object into a true object reference back.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class PtrConverter<T>
  {
    delegate U Void2ObjectConverter<U>(IntPtr pManagedObject);
    static Void2ObjectConverter<T> _myConverter;

    // The type initializer is run every time the converter is instantiated with a different 
    // generic argument. 
    static PtrConverter()
    {
      GenerateDynamicMethod();
    }

    static void GenerateDynamicMethod()
    {
      if (_myConverter == null)
      {
        DynamicMethod method = new DynamicMethod("ConvertPtrToObjReference", typeof(T),
                       new[] { typeof(IntPtr) }, StaticModule.UnsafeModule);
        var gen = method.GetILGenerator();
        // Load first argument 
        gen.Emit(OpCodes.Ldarg_0);
        // return it directly. The Clr will take care of the cast!
        // this construct is unverifiable so we need to plug this into an assembly with 
        // IL Verification disabled
        gen.Emit(OpCodes.Ret);
        _myConverter = (Void2ObjectConverter<T>)method.CreateDelegate(typeof(Void2ObjectConverter<T>));
      }
    }

    /// <summary>
    /// Convert a pointer to a managed object back to the original object reference
    /// </summary>
    /// <param name="pObj">Pointer to managed object</param>
    /// <returns>Object reference</returns>
    /// <exception cref="ExecutionEngineException">When the pointer does not point to valid CLR object. This can happen when the GC decides to move object references to new memory locations. 
    /// Beware this possibility exists all the time (although the probability should be very low)!</exception>
    public T ConvertFromIntPtr(IntPtr pObj)
    {
      return _myConverter(pObj);
    }
  }

  class StaticModule
  {
    private class Hack
    {
      
    }

    private static Hack _hack = new Hack();
    private static readonly string ModuleAssemblyName = Path.GetFileName(_hack.GetType().Assembly.Location);

    static Module _myUnsafeModule;
    public static Module UnsafeModule
    {
      get
      {
        if (_myUnsafeModule == null)
        {
          AssemblyName assemblyName = new AssemblyName(ModuleAssemblyName);
          AssemblyBuilder aBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName,
                                                                       AssemblyBuilderAccess.Run);
          ModuleBuilder mBuilder = aBuilder.DefineDynamicModule(ModuleAssemblyName);
          // set SkipVerification=true on our assembly to prevent VerificationExceptions which warn
          // about unsafe things but we want to do unsafe things after all.
          Type secAttrib = typeof(SecurityPermissionAttribute);
          var secCtor = secAttrib.GetConstructor(new Type[] { typeof(SecurityAction) });
          CustomAttributeBuilder attribBuilder = new CustomAttributeBuilder(secCtor,
              new object[] { SecurityAction.Assert },
              new PropertyInfo[] { secAttrib.GetProperty("SkipVerification", BindingFlags.Instance | BindingFlags.Public) },
              new object[] { true });

          aBuilder.SetCustomAttribute(attribBuilder);
          TypeBuilder tb = mBuilder.DefineType("MyDynamicType", TypeAttributes.Public);
          _myUnsafeModule = tb.CreateType().Module;
        }

        return _myUnsafeModule;
      }
    }
  }
}