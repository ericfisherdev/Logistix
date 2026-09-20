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

    [Fact]
    public void Reports_missing_method_for_a_target_inherited_rather_than_declared()
    {
        var result = SingleResultFor<Fixtures.InheritedMemberPatch>();

        Assert.Equal(PatchTargetOutcome.MissingMethod, result.Outcome);
    }

    [Fact]
    public void Records_no_skipped_type_errors_for_the_fixture_assembly()
    {
        _checker.CheckAssembly(typeof(Fixtures.ResolvedPatch).Assembly);

        Assert.Empty(_checker.SkippedTypeErrors);
    }

    [Fact]
    public void Resolves_a_constructor_patch()
    {
        var result = SingleResultFor<Fixtures.InstanceConstructorPatch>();

        Assert.Equal(PatchTargetOutcome.Resolved, result.Outcome);
        Assert.Contains(".ctor(Int32)", result.Detail);
    }

    [Fact]
    public void Resolves_a_static_constructor_patch()
    {
        var result = SingleResultFor<Fixtures.StaticConstructorPatch>();

        Assert.Equal(PatchTargetOutcome.Resolved, result.Outcome);
    }

    [Fact]
    public void Resolves_a_property_getter_patch()
    {
        var result = SingleResultFor<Fixtures.PropertyGetterPatch>();

        Assert.Equal(PatchTargetOutcome.Resolved, result.Outcome);
        Assert.Contains("get_Value", result.Detail);
    }

    [Fact]
    public void Resolves_a_property_setter_patch()
    {
        var result = SingleResultFor<Fixtures.PropertySetterPatch>();

        Assert.Equal(PatchTargetOutcome.Resolved, result.Outcome);
        Assert.Contains("set_Value", result.Detail);
    }

    [Fact]
    public void Reports_a_string_typed_declaring_type_as_unsupported_rather_than_missing()
    {
        var result = SingleResultFor<Fixtures.TypeNamePatch>();

        Assert.Equal(PatchTargetOutcome.Unsupported, result.Outcome);
        Assert.True(result.Success);
    }

    private PatchTargetResult SingleResultFor<TPatch>()
    {
        var results = _checker.CheckAssembly(typeof(TPatch).Assembly);
        return Assert.Single(results, r => r.PatchSite.Contains(typeof(TPatch).Name));
    }
}
