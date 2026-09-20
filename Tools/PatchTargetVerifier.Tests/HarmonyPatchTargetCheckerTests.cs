using Xunit;

namespace Logistix.Tools.PatchTargetVerifier.Tests;

public class HarmonyPatchTargetCheckerTests
{
    private readonly HarmonyPatchTargetChecker _checker = new();

    [Fact]
    public void Resolves_a_method_level_type_and_name_patch()
    {
        var result = SingleResultFor<Fixtures.ResolvedPatch>();

        Assert.Equal(PatchTargetOutcome.Resolved, result.Outcome);
        Assert.Contains("FakeGameType.Foo", result.Detail);
    }

    [Fact]
    public void Reports_missing_method_when_the_target_no_longer_exists()
    {
        var result = SingleResultFor<Fixtures.MissingMethodPatch>();

        Assert.Equal(PatchTargetOutcome.MissingMethod, result.Outcome);
    }

    [Fact]
    public void Reports_ambiguous_when_no_argument_types_narrow_an_overload()
    {
        var result = SingleResultFor<Fixtures.AmbiguousOverloadPatch>();

        Assert.Equal(PatchTargetOutcome.AmbiguousOverload, result.Outcome);
    }

    [Fact]
    public void Resolves_a_specific_overload_when_argument_types_are_given()
    {
        var result = SingleResultFor<Fixtures.ResolvedWithArgumentTypesPatch>();

        Assert.Equal(PatchTargetOutcome.Resolved, result.Outcome);
        Assert.Contains("Bar(Int32)", result.Detail);
    }

    [Fact]
    public void Merges_a_class_level_declaring_type_with_a_method_level_name()
    {
        var result = SingleResultFor<Fixtures.ClassLevelMergePatch>();

        Assert.Equal(PatchTargetOutcome.Resolved, result.Outcome);
        Assert.Contains("FakeGameType.Foo", result.Detail);
    }

    [Fact]
    public void Ignores_methods_with_no_harmony_role_attribute()
    {
        var results = _checker.CheckAssembly(typeof(Fixtures.NotAPatchAtAll).Assembly);

        Assert.DoesNotContain(results, r => r.PatchSite.Contains(nameof(Fixtures.NotAPatchAtAll)));
    }

    [Fact]
    public void Recognises_a_convention_named_patch_method_with_no_role_attribute()
    {
        var result = SingleResultFor<Fixtures.ConventionNamedPatch>();

        Assert.Equal(PatchTargetOutcome.MissingMethod, result.Outcome);
    }

    private PatchTargetResult SingleResultFor<TPatch>()
    {
        var results = _checker.CheckAssembly(typeof(TPatch).Assembly);
        return Assert.Single(results, r => r.PatchSite.Contains(typeof(TPatch).Name));
    }
}
