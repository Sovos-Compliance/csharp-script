namespace CSharpCodeEvaluatorTestClass
{
  // ReSharper disable once InconsistentNaming
  public class TestClassInGACAssembly
  {
    public int a;

    public TestClassInGACAssembly()
    {
      a = 10;
    }

    public int Inc()
    {
      return ++a;
    }
  }
}
