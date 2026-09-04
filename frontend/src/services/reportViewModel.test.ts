import { getComplexityDistribution, getTopComplexWorkflows, getTopIssues, getWorkflowHealth, isReportFormat, type AnalysisResponse, type Finding } from './reportViewModel';

describe('reportViewModel', () => {
  it('orders top issues by severity', () => {
    const findings: Finding[] = [
      finding('RPA008', 'Suggestion'),
      finding('RPA001', 'Warning'),
      finding('RPA002', 'Error'),
      finding('RPA009', 'Critical'),
    ];

    expect(getTopIssues(findings).map((item) => item.severity)).toEqual(['Critical', 'Error', 'Warning', 'Suggestion']);
  });

  it('sorts workflow health by finding count', () => {
    const analysis: AnalysisResponse = {
      analysis: {
        findings: [
          finding('RPA001', 'Warning', 'B.xaml'),
          finding('RPA002', 'Error', 'B.xaml'),
          finding('RPA003', 'Error', 'A.xaml'),
        ],
      },
      workflows: [
        { relativePath: 'A.xaml', activityCount: 3 },
        { relativePath: 'B.xaml', activityCount: 5 },
      ],
    };

    expect(getWorkflowHealth(analysis).map((workflow) => workflow.relativePath)).toEqual(['B.xaml', 'A.xaml']);
  });

  it('checks report formats', () => {
    expect(isReportFormat('json')).toBe(true);
    expect(isReportFormat('html')).toBe(true);
    expect(isReportFormat('pdf')).toBe(false);
  });

  it('summarizes workflow complexity', () => {
    const analysis: AnalysisResponse = {
      workflows: [
        { relativePath: 'A.xaml', activityCount: 3, complexity: { complexityScore: 20, complexityLevel: 'Low' } },
        { relativePath: 'B.xaml', activityCount: 50, complexity: { complexityScore: 120, complexityLevel: 'High' } },
        { relativePath: 'C.xaml', activityCount: 75, complexity: { complexityScore: 180, complexityLevel: 'VeryHigh' } },
      ],
    };

    expect(getComplexityDistribution(analysis)).toEqual({ Low: 1, High: 1, VeryHigh: 1 });
    expect(getTopComplexWorkflows(analysis, 2).map((workflow) => workflow.relativePath)).toEqual(['C.xaml', 'B.xaml']);
  });
});

function finding(ruleId: string, severity: string, workflowPath = 'Main.xaml'): Finding {
  return {
    ruleId,
    severity,
    workflowPath,
    ruleName: 'Rule',
    message: 'Message',
  };
}
