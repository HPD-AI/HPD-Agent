using Xunit;
using HPD.Agent.Internal.Filters;
using HPD.Agent;
using Microsoft.Extensions.AI;

namespace HPD.Agent.Tests.Filters;

public class ScopedFilterSystemTests
{
    private class TestFilter : IAiFunctionFilter
    {
        public string Name { get; }
        public bool WasCalled { get; private set; }

        public TestFilter(string name) => Name = name;

        public Task InvokeAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            WasCalled = true;
            return next(context);
        }
    }

    #region FilterScope Enum Tests

    [Fact]
    public void FilterScope_HasCorrectValues()
    {
        // Ensure scope values are in correct order for priority
        Assert.Equal(0, (int)FilterScope.Global);
        Assert.Equal(1, (int)FilterScope.Plugin);
        Assert.Equal(2, (int)FilterScope.Skill);
        Assert.Equal(3, (int)FilterScope.Function);
    }

    #endregion

    #region ScopedFilter AppliesTo Tests

    [Fact]
    public void GlobalFilter_AppliesToAllFunctions()
    {
        // Arrange
        var filter = new ScopedFilter(new TestFilter("global"), FilterScope.Global, null);

        // Act & Assert
        Assert.True(filter.AppliesTo("AnyFunction"));
        Assert.True(filter.AppliesTo("AnyFunction", "AnyPlugin"));
        Assert.True(filter.AppliesTo("AnyFunction", "AnyPlugin", "AnySkill"));
        Assert.True(filter.AppliesTo("AnyFunction", null, null, true));
    }

    [Fact]
    public void PluginFilter_AppliesToPluginFunctions()
    {
        // Arrange
        var filter = new ScopedFilter(new TestFilter("plugin"), FilterScope.Plugin, "FileSystemPlugin");

        // Act & Assert
        Assert.True(filter.AppliesTo("ReadFile", "FileSystemPlugin"));
        Assert.False(filter.AppliesTo("ReadFile", "DatabasePlugin"));
        Assert.False(filter.AppliesTo("ReadFile", null));
    }

    [Fact]
    public void SkillFilter_AppliesToSkillContainer()
    {
        // Arrange
        var filter = new ScopedFilter(new TestFilter("skill"), FilterScope.Skill, "analyze_codebase");

        // Act - skill container itself
        var applies = filter.AppliesTo(
            functionName: "analyze_codebase",
            pluginTypeName: null,
            skillName: null,
            isSkillContainer: true
        );

        // Assert
        Assert.True(applies);
    }

    [Fact]
    public void SkillFilter_AppliesToReferencedFunctions()
    {
        // Arrange
        var filter = new ScopedFilter(new TestFilter("skill"), FilterScope.Skill, "analyze_codebase");

        // Act - function called by skill
        var applies = filter.AppliesTo(
            functionName: "ReadFile",
            pluginTypeName: "FileSystemPlugin",
            skillName: "analyze_codebase",
            isSkillContainer: false
        );

        // Assert
        Assert.True(applies);
    }

    [Fact]
    public void SkillFilter_DoesNotApplyToOtherSkillContainers()
    {
        // Arrange
        var filter = new ScopedFilter(new TestFilter("skill"), FilterScope.Skill, "analyze_codebase");

        // Act - different skill container
        var applies = filter.AppliesTo(
            functionName: "refactor_code",
            pluginTypeName: null,
            skillName: null,
            isSkillContainer: true
        );

        // Assert
        Assert.False(applies);
    }

    [Fact]
    public void SkillFilter_DoesNotApplyToFunctionsFromOtherSkills()
    {
        // Arrange
        var filter = new ScopedFilter(new TestFilter("skill"), FilterScope.Skill, "analyze_codebase");

        // Act - function called by different skill
        var applies = filter.AppliesTo(
            functionName: "WriteFile",
            pluginTypeName: "FileSystemPlugin",
            skillName: "refactor_code",
            isSkillContainer: false
        );

        // Assert
        Assert.False(applies);
    }

    [Fact]
    public void FunctionFilter_AppliesToSpecificFunction()
    {
        // Arrange
        var filter = new ScopedFilter(new TestFilter("function"), FilterScope.Function, "ReadFile");

        // Act & Assert
        Assert.True(filter.AppliesTo("ReadFile"));
        Assert.False(filter.AppliesTo("WriteFile"));
    }

    #endregion

    #region ScopedFilterManager Tests

    [Fact]
    public void ScopedFilterManager_RegisterFunctionSkill_StoresMapping()
    {
        // Arrange
        var manager = new ScopedFilterManager();
        var filter = new TestFilter("skill");

        manager.AddFilter(filter, FilterScope.Skill, "analyze_codebase");
        manager.RegisterFunctionSkill("ReadFile", "analyze_codebase");

        // Act - No skill name provided, should use fallback
        var applicable = manager.GetApplicableFilters("ReadFile").ToList();

        // Assert
        Assert.Contains(filter, applicable);
    }

    [Fact]
    public void ScopedFilterManager_GetApplicableFilters_ReturnsCorrectFilters()
    {
        // Arrange
        var manager = new ScopedFilterManager();
        var globalFilter = new TestFilter("Global");
        var pluginFilter = new TestFilter("Plugin");
        var skillFilter = new TestFilter("Skill");
        var functionFilter = new TestFilter("Function");

        manager.AddFilter(globalFilter, FilterScope.Global, null);
        manager.AddFilter(pluginFilter, FilterScope.Plugin, "FileSystemPlugin");
        manager.AddFilter(skillFilter, FilterScope.Skill, "analyze_codebase");
        manager.AddFilter(functionFilter, FilterScope.Function, "ReadFile");

        manager.RegisterFunctionPlugin("ReadFile", "FileSystemPlugin");
        manager.RegisterFunctionSkill("ReadFile", "analyze_codebase");

        // Act
        var filters = manager.GetApplicableFilters(
            "ReadFile",
            "FileSystemPlugin",
            "analyze_codebase",
            false
        ).ToList();

        // Assert - all 4 filters should apply
        Assert.Equal(4, filters.Count);
        Assert.Contains(globalFilter, filters);
        Assert.Contains(pluginFilter, filters);
        Assert.Contains(skillFilter, filters);
        Assert.Contains(functionFilter, filters);
    }

    [Fact]
    public void ScopedFilterManager_GetApplicableFilters_OrdersByScope()
    {
        // Arrange
        var manager = new ScopedFilterManager();
        var globalFilter = new TestFilter("Global");
        var pluginFilter = new TestFilter("Plugin");
        var skillFilter = new TestFilter("Skill");
        var functionFilter = new TestFilter("Function");

        manager.AddFilter(functionFilter, FilterScope.Function, "ReadFile");
        manager.AddFilter(globalFilter, FilterScope.Global, null);
        manager.AddFilter(skillFilter, FilterScope.Skill, "analyze_codebase");
        manager.AddFilter(pluginFilter, FilterScope.Plugin, "FileSystemPlugin");

        manager.RegisterFunctionPlugin("ReadFile", "FileSystemPlugin");
        manager.RegisterFunctionSkill("ReadFile", "analyze_codebase");

        // Act
        var filters = manager.GetApplicableFilters(
            "ReadFile",
            "FileSystemPlugin",
            "analyze_codebase",
            false
        ).ToList();

        // Assert - OrderBy(scope) returns: Global(0) → Plugin(1) → Skill(2) → Function(3)
        Assert.Equal(4, filters.Count);
        Assert.Same(globalFilter, filters[0]);
        Assert.Same(pluginFilter, filters[1]);
        Assert.Same(skillFilter, filters[2]);
        Assert.Same(functionFilter, filters[3]);
    }

    [Fact]
    public void ScopedFilterManager_FallbackLookup_FindsPluginFromMapping()
    {
        // Arrange
        var manager = new ScopedFilterManager();
        var filter = new TestFilter("plugin");

        manager.AddFilter(filter, FilterScope.Plugin, "FileSystemPlugin");
        manager.RegisterFunctionPlugin("ReadFile", "FileSystemPlugin");

        // Act - No pluginTypeName provided, should use fallback
        var applicable = manager.GetApplicableFilters("ReadFile").ToList();

        // Assert
        Assert.Contains(filter, applicable);
    }

    [Fact]
    public void ScopedFilterManager_FallbackLookup_FindsSkillFromMapping()
    {
        // Arrange
        var manager = new ScopedFilterManager();
        var filter = new TestFilter("skill");

        manager.AddFilter(filter, FilterScope.Skill, "analyze_codebase");
        manager.RegisterFunctionSkill("ReadFile", "analyze_codebase");

        // Act - No skillName provided, should use fallback
        var applicable = manager.GetApplicableFilters("ReadFile").ToList();

        // Assert
        Assert.Contains(filter, applicable);
    }

    [Fact]
    public void ScopedFilterManager_SkillContainerInvocation_DoesNotRequireSkillName()
    {
        // Arrange
        var manager = new ScopedFilterManager();
        var filter = new TestFilter("skill");

        manager.AddFilter(filter, FilterScope.Skill, "analyze_codebase");

        // Act - Skill container invocation (isSkillContainer=true, skillName=null)
        var applicable = manager.GetApplicableFilters(
            "analyze_codebase",
            pluginTypeName: null,
            skillName: null,
            isSkillContainer: true
        ).ToList();

        // Assert - filter should apply because container name matches
        Assert.Contains(filter, applicable);
    }

    #endregion

    #region BuilderScopeContext Tests

    [Fact]
    public void BuilderScopeContext_DefaultScope_IsGlobal()
    {
        // Arrange & Act
        var context = new BuilderScopeContext();

        // Assert
        Assert.Equal(FilterScope.Global, context.CurrentScope);
        Assert.Null(context.CurrentTarget);
    }

    [Fact]
    public void BuilderScopeContext_SetSkillScope_UpdatesScope()
    {
        // Arrange
        var context = new BuilderScopeContext();

        // Act
        context.SetSkillScope("analyze_codebase");

        // Assert
        Assert.Equal(FilterScope.Skill, context.CurrentScope);
        Assert.Equal("analyze_codebase", context.CurrentTarget);
    }

    [Fact]
    public void BuilderScopeContext_SetGlobalScope_ResetsScope()
    {
        // Arrange
        var context = new BuilderScopeContext();
        context.SetSkillScope("analyze_codebase");

        // Act
        context.SetGlobalScope();

        // Assert
        Assert.Equal(FilterScope.Global, context.CurrentScope);
        Assert.Null(context.CurrentTarget);
    }

    [Fact]
    public void BuilderScopeContext_SetPluginScope_UpdatesScope()
    {
        // Arrange
        var context = new BuilderScopeContext();

        // Act
        context.SetPluginScope("FileSystemPlugin");

        // Assert
        Assert.Equal(FilterScope.Plugin, context.CurrentScope);
        Assert.Equal("FileSystemPlugin", context.CurrentTarget);
    }

    [Fact]
    public void BuilderScopeContext_SetFunctionScope_UpdatesScope()
    {
        // Arrange
        var context = new BuilderScopeContext();

        // Act
        context.SetFunctionScope("ReadFile");

        // Assert
        Assert.Equal(FilterScope.Function, context.CurrentScope);
        Assert.Equal("ReadFile", context.CurrentTarget);
    }

    #endregion

    #region Integration-style Tests

    [Fact]
    public void SkillFilter_AppliesToBothContainerAndReferencedFunctions()
    {
        // Arrange
        var manager = new ScopedFilterManager();
        var filter = new TestFilter("skill");

        manager.AddFilter(filter, FilterScope.Skill, "analyze_codebase");
        manager.RegisterFunctionSkill("ReadFile", "analyze_codebase");
        manager.RegisterFunctionSkill("ListDirectory", "analyze_codebase");

        // Act & Assert - skill container
        var containerApplicable = manager.GetApplicableFilters(
            "analyze_codebase",
            null,
            null,
            isSkillContainer: true
        ).ToList();
        Assert.Contains(filter, containerApplicable);

        // Act & Assert - referenced function 1
        var func1Applicable = manager.GetApplicableFilters("ReadFile").ToList();
        Assert.Contains(filter, func1Applicable);

        // Act & Assert - referenced function 2
        var func2Applicable = manager.GetApplicableFilters("ListDirectory").ToList();
        Assert.Contains(filter, func2Applicable);

        // Act & Assert - non-referenced function
        var otherApplicable = manager.GetApplicableFilters("WriteFile").ToList();
        Assert.DoesNotContain(filter, otherApplicable);
    }

    [Fact]
    public void MultipleSkills_CanReferenceeSameFunction()
    {
        // Arrange
        var manager = new ScopedFilterManager();
        var skill1Filter = new TestFilter("skill1");
        var skill2Filter = new TestFilter("skill2");

        manager.AddFilter(skill1Filter, FilterScope.Skill, "analyze_codebase");
        manager.AddFilter(skill2Filter, FilterScope.Skill, "refactor_code");

        // Both skills reference ReadFile
        manager.RegisterFunctionSkill("ReadFile", "analyze_codebase");
        // Note: In real scenario, mapping is 1:1, last one wins
        // But we're testing that different skills can have different filters

        // Act
        var skill1Applicable = manager.GetApplicableFilters(
            "ReadFile",
            skillName: "analyze_codebase"
        ).ToList();

        var skill2Applicable = manager.GetApplicableFilters(
            "ReadFile",
            skillName: "refactor_code"
        ).ToList();

        // Assert
        Assert.Contains(skill1Filter, skill1Applicable);
        Assert.DoesNotContain(skill2Filter, skill1Applicable);

        Assert.Contains(skill2Filter, skill2Applicable);
        Assert.DoesNotContain(skill1Filter, skill2Applicable);
    }

    #endregion
}
