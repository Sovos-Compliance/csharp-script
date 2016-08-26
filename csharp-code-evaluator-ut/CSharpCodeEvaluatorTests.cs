using System.Collections.Generic;
using System.Dynamic;
using NUnit.Framework;
using Sovos.CSharpCodeEvaluator;

namespace csharp_code_evaluator_ut
{
  public class TestClass
  {
    public int int_Field;
  }

  public class CSharpCodeEvaluatorTests
  {
    [Test]
    public void BasicExpression_Success()
    {
      var expression = new CSharpExpression("1 + 1");
      Assert.AreEqual(2, expression.execute());
    }

    [Test]
    public void ExpressionWithOneParameter_Success()
    {
      var expression = new CSharpExpression("1 + a");
      expression.addObjectInScope("a", 1);
      Assert.AreEqual(2, expression.execute());
    }

    [Test]
    public void ExpressionWithOneParameterForceRecompilation_Success()
    {
      var expression = new CSharpExpression("1 + a");
      expression.addObjectInScope("a", 1);
      Assert.AreEqual(2, expression.execute());
      expression.addObjectInScope("b", 2);
      Assert.AreEqual(2, expression.execute());
    }

    [Test]
    public void ExpressionWithTwoParameters_Success()
    {
      var expression = new CSharpExpression("1 + a + c");
      expression.addObjectInScope("a", 1);
      expression.addObjectInScope("c", 3);
      Assert.AreEqual(5, expression.execute());
    }

    [Test]
    [ExpectedException("Sovos.CSharpCodeEvaluator.ECSharpExpression")]
    public void ExpressionWithSameParameterTwice_Fails()
    {
      var expression = new CSharpExpression("1 + a + a");
      expression.addObjectInScope("a", 1);
      expression.addObjectInScope("a", 3);
      Assert.AreEqual(5, expression.execute());
    }

    [Test]
    public void ExpressionReferencingLocalObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      var obj = new TestClass {int_Field = 2};
      expression.addObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.execute());
    }

    [Test]
    public void ExpressionReferencingTwoLocalObjects_Success()
    {
      var expression = new CSharpExpression("obj2.int_Field + obj1.int_Field");
      var obj1 = new TestClass { int_Field = 2 };
      var obj2 = new TestClass { int_Field = 3 };
      expression.addObjectInScope("obj1", obj1);
      expression.addObjectInScope("obj2", obj2);
      Assert.AreEqual(5, expression.execute());
    }

    [Test]
    public void ExecuteBasicExpressionTwice_Success()
    {
      var expression = new CSharpExpression("1 + 1");
      Assert.AreEqual(2, expression.execute());
      Assert.AreEqual(2, expression.execute());
    }

    [Test]
    public void ExpressionReferencingMutableLocalObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      var obj = new TestClass { int_Field = 2 };
      expression.addObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.execute());
      obj.int_Field = 5;
      Assert.AreEqual(6, expression.execute());
    }

    [Test]
    public void ExpressionReferencingExpandoObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      IDictionary<string, object> obj = new ExpandoObject();
      obj.Add("int_Field", 2);
      expression.addObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.execute());
    }

    [Test]
    public void ExpressionReferencingMutableExpandoObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      IDictionary<string, object> obj = new ExpandoObject();
      obj.Add("int_Field", 2);
      expression.addObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.execute());
      dynamic dynObj = obj;
      dynObj.int_Field = 3;
      Assert.AreEqual(4, expression.execute());
    }
  }
}
