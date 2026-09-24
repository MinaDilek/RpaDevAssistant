import React from 'react';
import { Send } from 'lucide-react';
import type { ProjectAnswer } from '../services/reportViewModel';

export function AskProjectPanel({
  question,
  setQuestion,
  history,
  isLoading,
  t,
  onAsk,
}: {
  question: string;
  setQuestion: (value: string) => void;
  history: Array<{ question: string; answer: ProjectAnswer }>;
  isLoading: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onAsk: (question?: string) => void;
}) {
  const suggestedQuestions = [
    t('suggestedQueueQuestion'),
    t('suggestedDelayQuestion'),
    t('suggestedMostFindingsQuestion'),
    t('suggestedProcessQuestion'),
    t('suggestedInvocationQuestion'),
    t('suggestedExceptionQuestion'),
  ];

  return (
    <div className="ask-panel">
      <p className="notice">{t('askNotice')}</p>
      <section className="ask-box" aria-label={t('askLabel')}>
        <label htmlFor="projectQuestion">{t('askLabel')}</label>
        <textarea
          id="projectQuestion"
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          maxLength={2000}
          placeholder={t('askPlaceholder')}
        />
        <button
          type="button"
          onClick={() => onAsk()}
          disabled={isLoading || !question.trim()}
        >
          <Send size={18} />
          {isLoading ? t('asking') : t('ask')}
        </button>
      </section>

      {history.length === 0 && (
        <section>
          <h2>{t('suggestedQuestions')}</h2>
          <div className="suggestions">
            {suggestedQuestions.map((suggestion) => (
              <button
                key={suggestion}
                type="button"
                onClick={() => onAsk(suggestion)}
                disabled={isLoading}
              >
                {suggestion}
              </button>
            ))}
          </div>
        </section>
      )}

      <section className="answer-list">
        {history.map((item) => (
          <ProjectAnswerCard
            key={`${item.question}-${item.answer.generatedAtUtc ?? item.answer.answer}`}
            question={item.question}
            answer={item.answer}
            t={t}
          />
        ))}
      </section>
    </div>
  );
}

export function ProjectAnswerCard({
  question,
  answer,
  t,
}: {
  question: string;
  answer: ProjectAnswer;
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  return (
    <article className="answer-card">
      <span className="question-label">{question}</span>
      <h2>{t('answer')}</h2>
      <p>{answer.answer}</p>
      <div className="answer-meta">
        <span>
          {t('questionConfidence')}: {answer.confidence}
        </span>
        <span>
          {t('aiUsed')}: {answer.usedAi ? t('yes') : t('no')}
        </span>
        <span>{answer.usedAi ? t('aiAnswerBased') : t('answeredLocal')}</span>
      </div>
      {(answer.relatedWorkflows ?? []).length > 0 && (
        <p>
          <strong>{t('relatedWorkflows')}:</strong> {answer.relatedWorkflows!.join(', ')}
        </p>
      )}
      {(answer.relatedRuleIds ?? []).length > 0 && (
        <p>
          <strong>{t('relatedRules')}:</strong> {answer.relatedRuleIds!.join(', ')}
        </p>
      )}
      {answer.reasoningSummary && (
        <p>
          <strong>{t('reasoningSummary')}:</strong> {answer.reasoningSummary}
        </p>
      )}
      {answer.errorMessage && <p className="error-text">{answer.errorMessage}</p>}
      <details>
        <summary>
          {t('evidence')} ({answer.evidence?.length ?? 0})
        </summary>
        <div className="evidence-list">
          {(answer.evidence ?? []).map((evidence, index) => (
            <div
              className="evidence-row"
              key={`${evidence.type}-${evidence.workflowPath}-${evidence.activityName}-${evidence.ruleId}-${index}`}
            >
              <strong>{evidence.type}</strong>
              {evidence.workflowPath && <span>Workflow: {evidence.workflowPath}</span>}
              {(evidence.activityDisplayName || evidence.activityName) && (
                <span>
                  {t('activity')}: {evidence.activityDisplayName ?? evidence.activityName}
                </span>
              )}
              {evidence.ruleId && (
                <span>
                  {t('rule')}: {evidence.ruleId}
                </span>
              )}
              {evidence.propertyName && (
                <span>
                  {t('property')}: {evidence.propertyName}
                </span>
              )}
              {evidence.value && (
                <span>
                  {t('value')}: {evidence.value}
                </span>
              )}
              {evidence.description && <p>{evidence.description}</p>}
            </div>
          ))}
        </div>
      </details>
    </article>
  );
}
