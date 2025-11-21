using System;
using System.Collections.Generic;

/// <summary>
/// Financial Analysis Skills - Orchestrates FinancialAnalysisPlugin functions into semantic workflows
/// Each skill groups related functions that are consistently used together
/// SOPs are documented in Skills/SOPs/ directory
/// </summary>
public class FinancialAnalysisSkills
{
    /// <summary>
    /// Quick Liquidity Analysis Skill
    /// Analyzes a company's ability to meet short-term obligations
    /// </summary>
    [Skill(Category = "Liquidity Analysis", Priority = 10)]
    public Skill QuickLiquidityAnalysis(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "QuickLiquidityAnalysis",
            description: "Analyze company's short-term liquidity position using current ratio, quick ratio, and working capital. Calls base FinancialAnalysisPlugin functions directly.",
            instructions: @"
📋 QUICK LIQUIDITY ANALYSIS - EXECUTION PROTOCOL

Use this skill to assess whether a company can pay its short-term obligations.

⚠️ FOR DETAILED GUIDANCE:
→ read_skill_document('01-quickliquidityanalysis-sop')

═══════════════════════════════════════════════════════════
PARALLEL EXECUTION (All metrics are independent):
═══════════════════════════════════════════════════════════

🚀 Execute ALL metrics SIMULTANEOUSLY for optimal performance:

→ Call: FinancialAnalysisPlugin.CalculateCurrentRatio(currentAssets, currentLiabilities)
   Interpretation: >1.5 is generally healthy

→ Call: FinancialAnalysisPlugin.CalculateQuickRatio(currentAssets, currentLiabilities, inventory)
   Interpretation: >1.0 is conservative

→ Call: FinancialAnalysisPlugin.CalculateWorkingCapital(currentAssets, currentLiabilities)
   Interpretation: Positive indicates liquidity cushion

⚠️ These three functions have NO dependencies - execute in parallel for speed.

═══════════════════════════════════════════════════════════
After parallel execution completes, synthesize findings:
- Can company meet short-term obligations?
- Is liquidity position healthy vs. industry benchmarks?
- Any red flags requiring investigation?

For detailed interpretation, thresholds, and industry benchmarks:
→ read_skill_document('01-quickliquidityanalysis-sop')",
            options: new SkillOptions()
                .AddDocumentFromFile(
                    "./Skills/SOPs/01-QuickLiquidityAnalysis-SOP.md",
                    "Step-by-step procedure for analyzing liquidity ratios"),
            "FinancialAnalysisPlugin.CalculateCurrentRatio",
            "FinancialAnalysisPlugin.CalculateQuickRatio",
            "FinancialAnalysisPlugin.CalculateWorkingCapital"
        );
    }

    /// <summary>
    /// Capital Structure Analysis Skill
    /// Analyzes how the company finances itself (debt vs equity mix)
    /// </summary>
    [Skill(Category = "Leverage Analysis", Priority = 11)]
    public Skill CapitalStructureAnalysis(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "CapitalStructureAnalysis",
            description: "Analyze company's capital structure and financial leverage through debt and equity ratios. Calls base FinancialAnalysisPlugin functions directly.",
            instructions: @"
📋 CAPITAL STRUCTURE ANALYSIS - EXECUTION PROTOCOL

Use this skill to understand how the company is financed and assess financial risk.

⚠️ FOR DETAILED GUIDANCE:
→ read_skill_document('02-capitalstructureanalysis-sop')

═══════════════════════════════════════════════════════════
PARALLEL EXECUTION (All leverage ratios are independent):
═══════════════════════════════════════════════════════════

🚀 Execute ALL leverage metrics SIMULTANEOUSLY for optimal performance:

→ Call: FinancialAnalysisPlugin.CalculateDebtToEquityRatio(totalLiabilities, stockholdersEquity)
   Interpretation: >1.0 means more debt than equity (higher leverage)

→ Call: FinancialAnalysisPlugin.CalculateDebtToAssetsRatio(totalLiabilities, totalAssets)
   Interpretation: Shows what % of assets are financed by debt

→ Call: FinancialAnalysisPlugin.CalculateEquityMultiplier(totalAssets, stockholdersEquity)
   Interpretation: Part of DuPont analysis

→ Call: FinancialAnalysisPlugin.EquityToTotalAssetsPercentage(stockholdersEquity, totalAssets)
   Interpretation: Conservative companies have higher equity %

⚠️ These four functions have NO dependencies - execute in parallel for speed.

═══════════════════════════════════════════════════════════
After parallel execution completes, synthesize findings:
- What's the debt/equity mix?
- Is leverage appropriate for the industry?
- What's the financial risk level?

For detailed interpretation, industry benchmarks, and risk assessment:
→ read_skill_document('02-capitalstructureanalysis-sop')",
            options: new SkillOptions()
                .AddDocumentFromFile(
                    "./Skills/SOPs/02-CapitalStructureAnalysis-SOP.md",
                    "Step-by-step procedure for analyzing capital structure and leverage"),
            "FinancialAnalysisPlugin.CalculateDebtToEquityRatio",
            "FinancialAnalysisPlugin.CalculateDebtToAssetsRatio",
            "FinancialAnalysisPlugin.CalculateEquityMultiplier",
            "FinancialAnalysisPlugin.EquityToTotalAssetsPercentage"
        );
    }

    /// <summary>
    /// Period Change Analysis Skill
    /// Analyzes how financial metrics changed from one period to another
    /// </summary>
    [Skill(Category = "Trend Analysis", Priority = 12)]
    public Skill PeriodChangeAnalysis(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "PeriodChangeAnalysis",
            description: "Analyze period-over-period changes in financial metrics using absolute and relative measures. Calls base FinancialAnalysisPlugin functions directly.",
            instructions: @"
📋 PERIOD CHANGE ANALYSIS - EXECUTION PROTOCOL

Use this skill to understand how financial items changed between periods.

⚠️ FOR DETAILED GUIDANCE:
→ read_skill_document('03-periodchangeanalysis-sop')

═══════════════════════════════════════════════════════════
PARALLEL EXECUTION PER LINE ITEM:
═══════════════════════════════════════════════════════════

For each financial line item being analyzed, execute ALL three metrics in PARALLEL:

→ Call: FinancialAnalysisPlugin.CalculateAbsoluteChange(currentPeriodValue, priorPeriodValue)
   Shows: Raw dollar impact

→ Call: FinancialAnalysisPlugin.CalculatePercentageChange(currentPeriodValue, priorPeriodValue)
   Shows: Relative magnitude of change (growth rate)

→ Call: FinancialAnalysisPlugin.CalculatePercentagePointChange(currentPercent, priorPercent)
   Shows: Change in common-size percentages

⚠️ These three functions have NO dependencies - execute in parallel for speed.
⚠️ Process MULTIPLE line items in parallel - each line item's calculations are independent.

═══════════════════════════════════════════════════════════
When to use each metric:
- Absolute Change: Total dollar impact on balance sheet
- Percentage Change: Growth rate and relative magnitude
- Percentage Point Change: How much share of total changed

After completing all parallel calculations, synthesize the change story:
- What changed and by how much?
- Are changes favorable or concerning?
- What business drivers explain the changes?

For detailed interpretation and example scenarios:
→ read_skill_document('03-periodchangeanalysis-sop')",
            options: new SkillOptions()
                .AddDocumentFromFile(
                    "./Skills/SOPs/03-PeriodChangeAnalysis-SOP.md",
                    "Step-by-step procedure for analyzing period-over-period changes"),
            "FinancialAnalysisPlugin.CalculateAbsoluteChange",
            "FinancialAnalysisPlugin.CalculatePercentageChange",
            "FinancialAnalysisPlugin.CalculatePercentagePointChange"
        );
    }

    /// <summary>
    /// Common-Size Balance Sheet Skill
    /// Normalizes balance sheet items to percentages for comparison
    /// </summary>
    [Skill(Category = "Comparative Analysis", Priority = 13)]
    public Skill CommonSizeBalanceSheet(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "CommonSizeBalanceSheet",
            description: "Create a common-size balance sheet showing each item as a percentage of total assets. Calls base FinancialAnalysisPlugin functions directly.",
            instructions: @"
📋 COMMON-SIZE BALANCE SHEET - EXECUTION PROTOCOL

Use this skill to build a common-size balance sheet for comparison across periods or companies.

⚠️ FOR DETAILED GUIDANCE:
→ read_skill_document('04-commonsizebalancesheet-sop')

═══════════════════════════════════════════════════════════
PARALLEL EXECUTION (Asset and liability calculations are independent):
═══════════════════════════════════════════════════════════

🚀 Execute asset and liability calculations SIMULTANEOUSLY:

→ Call: FinancialAnalysisPlugin.CommonSizeBalanceSheetAssets(...)
   Shows: Each asset as % of total assets

→ Call: FinancialAnalysisPlugin.CommonSizeBalanceSheetLiabilities(...)
   Shows: Each liability as % of total liabilities

→ Call: FinancialAnalysisPlugin.EquityToTotalAssetsPercentage(stockholdersEquity, totalAssets)
   Shows: Capital structure

⚠️ Asset and liability calculations have NO dependencies - execute in parallel for speed.

SEQUENTIAL STEP: Verify percentages
→ All asset percentages should sum to 100%
→ All liability percentages should sum to 100%
→ Run this AFTER parallel calculations complete

═══════════════════════════════════════════════════════════
Benefits of common-size analysis:
- Compare companies of different sizes
- Identify structural changes over time
- Spot unusual asset/liability distributions

After verification completes:
- Identify any unusual concentrations
- Compare to industry benchmarks
- Flag any structural concerns

For detailed interpretation and industry comparisons:
→ read_skill_document('04-commonsizebalancesheet-sop')",
            options: new SkillOptions()
                .AddDocumentFromFile(
                    "./Skills/SOPs/04-CommonSizeBalanceSheet-SOP.md",
                    "Step-by-step procedure for creating common-size financial statements"),
            "FinancialAnalysisPlugin.CalculateCommonSizePercentage",
            "FinancialAnalysisPlugin.CommonSizeBalanceSheetAssets",
            "FinancialAnalysisPlugin.CommonSizeBalanceSheetLiabilities",
            "FinancialAnalysisPlugin.EquityToTotalAssetsPercentage"
        );
    }

    /// <summary>
    /// Financial Health Dashboard Skill
    /// Comprehensive financial analysis combining all analysis techniques
    /// </summary>
    [Skill(Category = "Executive Summary", Priority = 1)]
    public Skill FinancialHealthDashboard(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "FinancialHealthDashboard",
            description: "Comprehensive financial health assessment combining liquidity, leverage, common-size, and change analysis. Calls base FinancialAnalysisPlugin functions directly (NOT sub-skill containers).",
            instructions: @"
📋 FINANCIAL HEALTH DASHBOARD - EXECUTION PROTOCOL

Use this skill for a complete financial health assessment.

⚠️ FOR DETAILED GUIDANCE:
→ read_skill_document('05-financialhealthdashboard-sop')
→ read_skill_document('00-analysisframework-overview')

⚠️ CRITICAL RULES:
1. Call BASE FinancialAnalysisPlugin functions DIRECTLY
2. DO NOT activate sub-skill containers (QuickLiquidityAnalysis, CapitalStructureAnalysis, etc.)
3. Follow the execution sequence below - sequential prerequisite then parallel batches

═══════════════════════════════════════════════════════════
SEQUENTIAL PREREQUISITE:
═══════════════════════════════════════════════════════════

STEP 1: VALIDATE Balance Sheet
→ Call: FinancialAnalysisPlugin.ValidateBalanceSheetEquation(totalAssets, totalLiabilities, stockholdersEquity)
→ Purpose: Verify data integrity before analysis
→ ⚠️ WAIT for validation result before proceeding
→ If invalid, investigate data issues first

═══════════════════════════════════════════════════════════
PARALLEL EXECUTION BATCHES (After validation passes):
═══════════════════════════════════════════════════════════

🚀 PARALLEL BATCH 1: LIQUIDITY & LEVERAGE & STRUCTURE ANALYSIS
   All three analysis categories can run simultaneously

   LIQUIDITY METRICS (3 parallel calls):
   → FinancialAnalysisPlugin.CalculateCurrentRatio(currentAssets, currentLiabilities)
   → FinancialAnalysisPlugin.CalculateQuickRatio(currentAssets, currentLiabilities, inventory)
   → FinancialAnalysisPlugin.CalculateWorkingCapital(currentAssets, currentLiabilities)
   Purpose: Can company meet short-term obligations?

   LEVERAGE METRICS (3 parallel calls):
   → FinancialAnalysisPlugin.CalculateDebtToEquityRatio(totalLiabilities, stockholdersEquity)
   → FinancialAnalysisPlugin.CalculateDebtToAssetsRatio(totalLiabilities, totalAssets)
   → FinancialAnalysisPlugin.CalculateEquityMultiplier(totalAssets, stockholdersEquity)
   Purpose: What's the debt/equity mix? Is it sustainable?

   STRUCTURE METRICS (3 parallel calls):
   → FinancialAnalysisPlugin.CommonSizeBalanceSheetAssets(...)
   → FinancialAnalysisPlugin.CommonSizeBalanceSheetLiabilities(...)
   → FinancialAnalysisPlugin.EquityToTotalAssetsPercentage(stockholdersEquity, totalAssets)
   Purpose: How is the balance sheet distributed?

🚀 PARALLEL BATCH 2: TRENDS ANALYSIS (if comparing periods)
   All change metrics can run simultaneously

   → FinancialAnalysisPlugin.CalculateAbsoluteChange(currentAmount, priorAmount)
   → FinancialAnalysisPlugin.CalculatePercentageChange(currentAmount, priorAmount)
   Purpose: How did metrics change from prior period?

═══════════════════════════════════════════════════════════
🚫 PROHIBITED - DO NOT ACTIVATE THESE SUB-SKILLS:
═══════════════════════════════════════════════════════════
❌ QuickLiquidityAnalysis() - Use Batch 1 liquidity functions instead
❌ CapitalStructureAnalysis() - Use Batch 1 leverage functions instead
❌ PeriodChangeAnalysis() - Use Batch 2 trend functions instead
❌ CommonSizeBalanceSheet() - Use Batch 1 structure functions instead

These container skills are for STANDALONE use only when user requests
specific focused analysis. The Dashboard uses BASE FUNCTIONS directly.

═══════════════════════════════════════════════════════════
OUTPUT FORMAT:
═══════════════════════════════════════════════════════════
After completing ALL parallel batches, provide comprehensive synthesis:

1. Liquidity Assessment
   - Current liquidity position
   - Ability to meet short-term obligations
   - Red flags or concerns

2. Solvency Assessment
   - Capital structure analysis
   - Financial leverage evaluation
   - Debt sustainability

3. Structural Analysis
   - Balance sheet composition
   - Unusual concentrations
   - Asset/liability distribution

4. Trend Analysis (if periods compared)
   - Key changes from prior period
   - Favorable vs. concerning trends
   - Business trajectory

5. Overall Risk Rating
   - Low / Medium / High financial risk
   - Key strengths and weaknesses
   - Recommendations

For detailed interpretation, industry benchmarks, and analysis framework:
→ read_skill_document('05-financialhealthdashboard-sop')",
            options: new SkillOptions()
                .AddDocumentFromFile(
                    "./Skills/SOPs/05-FinancialHealthDashboard-SOP.md",
                    "Complete framework for comprehensive financial analysis")
                .AddDocumentFromFile(
                    "./Skills/SOPs/00-AnalysisFramework-Overview.md",
                    "Overview of all financial analysis skills and when to use them"),
            "FinancialAnalysisPlugin.ValidateBalanceSheetEquation",
            "FinancialAnalysisPlugin.CalculateCurrentRatio",
            "FinancialAnalysisPlugin.CalculateQuickRatio",
            "FinancialAnalysisPlugin.CalculateWorkingCapital",
            "FinancialAnalysisPlugin.CalculateDebtToEquityRatio",
            "FinancialAnalysisPlugin.CalculateDebtToAssetsRatio",
            "FinancialAnalysisPlugin.CalculateEquityMultiplier",
            "FinancialAnalysisPlugin.CommonSizeBalanceSheetAssets",
            "FinancialAnalysisPlugin.CommonSizeBalanceSheetLiabilities",
            "FinancialAnalysisPlugin.CalculateAbsoluteChange",
            "FinancialAnalysisPlugin.CalculatePercentageChange",
            "FinancialAnalysisPlugin.CalculatePercentagePointChange"
        );
    }
}