using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Sovos.Infrastructure
{
  public class SovosExpando : DynamicObject
  {
    public IDictionary<string, object> Dictionary { get; set; }

    public SovosExpando()
    {
      Dictionary = new Dictionary<string, object>();
    }

    public int Count { get { return Dictionary.Keys.Count; } }

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
      if (Dictionary.ContainsKey("Get" + binder.Name))
      {
        var del = (Delegate)Dictionary["Get" + binder.Name];
        result = del.DynamicInvoke(Dictionary);
        return true;
      }
      if (!Dictionary.ContainsKey(binder.Name))
        return base.TryGetMember(binder, out result); //means result = null and return = false
      result = Dictionary[binder.Name];
      return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
      if (Dictionary.ContainsKey("Set" + binder.Name))
      {
        var del = (Delegate)Dictionary["Set" + binder.Name];
        del.DynamicInvoke(Dictionary, value);
        return true;
      }
      if (!Dictionary.ContainsKey(binder.Name))
      {
        Dictionary.Add(binder.Name, value);
      }
      else
        Dictionary[binder.Name] = value;

      return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
    {
      if (!Dictionary.ContainsKey(binder.Name) || !(Dictionary[binder.Name] is Delegate))
        return base.TryInvokeMember(binder, args, out result);
      var del = (Delegate)Dictionary[binder.Name];
      var argsWithThis = new object[args.Length + 1];
      argsWithThis[0] = Dictionary;
      var i = 1;
      foreach (var arg in args)
        argsWithThis[i++] = arg;
      result = del.DynamicInvoke(argsWithThis);
      return true;
    }

    public override bool TryDeleteMember(DeleteMemberBinder binder)
    {
      if (!Dictionary.ContainsKey(binder.Name)) return base.TryDeleteMember(binder);
      Dictionary.Remove(binder.Name);
      return true;
    }

    public override IEnumerable<string> GetDynamicMemberNames()
    {
      foreach (string name in Dictionary.Keys)
        yield return name;
    }
  }

}