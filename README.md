# csharp-code-evaluator
Dynamically evaluate C# code

This repo provides class CSharpExpression that allows the developer to run a C# expression dynamically with full access to a set of provided objects.

The usage is very simple. Just create a CSharpExpression object, add "outside" objects in its scope, and they become accesible to the expression.

Example:

The container program declares a class:

```C#
public class TestClass
{
  public int int_Field;
}
```

later the following can be executed:

```C#
var expression = new CSharpExpression("1 + obj.int_Field");
var obj = new TestClass {int_Field = 2};
expression.AddObjectInScope("obj", obj);
Assert.AreEqual(3, expression.Execute());
```
CSharpExpression also support working with "dynamic" types via ExpandoObject.

the following also works:

```C#
var expression = new CSharpExpression("1 + obj.int_Field");
IDictionary<string, object> obj = new ExpandoObject();
obj.Add("int_Field", 2);
expression.AddObjectInScope("obj", obj);
Assert.AreEqual(3, expression.Execute());
```

  
  
