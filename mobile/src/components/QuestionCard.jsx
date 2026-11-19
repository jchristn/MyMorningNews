import React, { useState } from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { SCALE, answerBody } from '@/lib/wizard';
import { spacing } from '@/theme/palette';
import { ButtonRow, ChipGroup } from './Chips';
import { Badge, Banner, Button, Card, Field, T } from './ui';

function selectedIds(options) {
  return (options || []).filter((o) => o.selected).map((o) => o.id);
}

/**
 * One clarifying question (refinement wizard and one-time requests) of any kind: single or multi choice, a 1 to 5
 * scale, yes or no, or free text, each skippable. `onAnswer(body)` sends a QuestionAnswerRequest and resolves to the
 * updated `{ question, options }`. Local selections start from the item; give the card a `key` that changes with the
 * question to start over.
 */
export function QuestionCard({ item, onAnswer, disabled, index }) {
  const { t } = useTranslation();
  const q = item.question;
  const options = (item.options || []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
  const [ids, setIds] = useState(() => selectedIds(options));
  const [text, setText] = useState(q.answerText || '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const send = async (body) => {
    setBusy(true);
    setError(null);
    try {
      await onAnswer(body);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };
  const off = disabled || busy;
  let control = null;
  if (q.kind === 'SingleChoice' || q.kind === 'MultiChoice') {
    const multi = q.kind === 'MultiChoice';
    control = (
      <>
        <ChipGroup
          multiple={multi}
          disabled={off}
          options={options.map((o) => ({ value: o.id, label: o.label, testID: 'option-' + o.id }))}
          value={multi ? ids : ids[0] || null}
          onChange={(v) => {
            const next = multi ? v : [v];
            setIds(next);
            if (!multi) void send(answerBody(q.kind, { optionIds: next, text }));
          }}
          accessibilityLabel={q.questionText}
        />
        {multi ? <Button title={t('mobile.questions.saveAnswer')} kind="secondary" disabled={off || ids.length === 0} onPress={() => void send(answerBody(q.kind, { optionIds: ids, text }))} testID={'answer-' + q.id} /> : null}
      </>
    );
  } else if (q.kind === 'Scale') {
    control = (
      <View style={{ gap: spacing.xs }}>
        <ChipGroup
          disabled={off}
          options={SCALE.map((n) => ({ value: String(n), label: String(n), testID: 'scale-' + q.id + '-' + n }))}
          value={text}
          onChange={(v) => {
            setText(v);
            void send(answerBody(q.kind, { text: v }));
          }}
          accessibilityLabel={q.questionText}
        />
        {options.length > 1 ? <T variant="small">{t('mobile.questions.scaleEnds', { low: options[0].label, high: options[options.length - 1].label })}</T> : null}
      </View>
    );
  } else if (q.kind === 'YesNo') {
    control = (
      <ChipGroup
        disabled={off}
        options={[
          { value: 'yes', label: t('mobile.common.yes'), testID: 'yes-' + q.id },
          { value: 'no', label: t('mobile.common.no'), testID: 'no-' + q.id },
        ]}
        value={String(text || '').toLowerCase()}
        onChange={(v) => {
          setText(v);
          void send(answerBody(q.kind, { text: v }));
        }}
        accessibilityLabel={q.questionText}
      />
    );
  } else {
    control = (
      <>
        <Field label={t('mobile.questions.yourAnswer')} value={text} onChangeText={setText} multiline editable={!off} testID={'text-' + q.id} />
        <Button title={t('mobile.questions.saveAnswer')} kind="secondary" disabled={off || !text.trim()} onPress={() => void send(answerBody(q.kind, { text }))} testID={'answer-' + q.id} />
      </>
    );
  }

  return (
    <Card testID={'question-' + q.id}>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
        {q.answered ? <Badge label={t('mobile.questions.answered')} tone="success" /> : null}
        {q.skipped ? <Badge label={t('mobile.questions.skipped')} tone="neutral" /> : null}
      </View>
      <T style={{ fontWeight: '600' }}>{(typeof index === 'number' ? index + 1 + '. ' : '') + q.questionText}</T>
      {q.rationale ? <T variant="small">{t('mobile.questions.why', { text: q.rationale })}</T> : null}
      {control}
      {error ? <Banner kind="danger">{error}</Banner> : null}
      {!q.skipped ? (
        <ButtonRow>
          <Button title={t('mobile.questions.skip')} kind="ghost" disabled={off} onPress={() => void send(answerBody(q.kind, { skipped: true }))} testID={'skip-' + q.id} />
        </ButtonRow>
      ) : null}
    </Card>
  );
}
