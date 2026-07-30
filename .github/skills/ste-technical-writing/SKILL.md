---
name: ste-technical-writing
description: 'Write or rewrite technical documentation under ASD-STE100 (Simplified Technical English) rules. Use for generating hyper-clear, unambiguous, machine-readable docs with strict vocabulary, active voice, and short sentences. Provides three modes: Rewrite, Generate, and Lint.'
argument-hint: 'Mode: rewrite, generate, or lint — plus the input text or feature to document'
---

# STE Technical Writing

## Outcome

Produce technical documentation that follows ASD-STE100 (Simplified Technical English) rules. The output must be:

- lean, instructional, and immediately actionable
- free of marketing fluff, hedging, and complex phrasing
- consistent in vocabulary: one noun per object, one verb per action
- machine-readable and unambiguous

## When to Use

Use this skill when you need to:

- rewrite existing documentation to remove "AI slop" (superfluous adjectives, hedging, complex phrasing)
- generate new STE-compliant documentation for a feature, API, or code module
- lint and review existing text for STE violations and suggest corrections
- enforce a consistent technical writing style across project documentation
- produce procedural instructions, configuration guides, or reference docs

Do not use this skill for conversational chat, creative writing, or user-facing marketing copy.

## Operational Modes

The skill provides three modes of operation. Specify one mode when invoking.

### 1. Rewrite Mode

Rewrite existing text to comply with STE rules.

- Compress sentences to 20 words or fewer for procedures, 25 words for descriptions.
- Limit paragraphs to 6 sentences or fewer.
- Use only terminal punctuation (periods, question marks) and commas. Do not use semicolons or em-dashes.
- Use active voice exclusively. (Example: "The server sends the data." NOT "The data is sent by the server.")
- Replace phrasal verbs with single-word alternatives. (Example: "create" replaces "spin up". "determine" replaces "figure out".)
- Remove all subjective adjectives and adverbs. (Example: remove "seamless", "robust", "powerful", "cutting-edge", "highly", "easily".)
- Remove all hedging and modals. (Example: remove "might", "could", "should", "potentially".)
- Do not rotate synonyms. Pick one term and use it consistently.
- Do not string more than three nouns together. (Example: "the module for system configuration" NOT "the system configuration module".)
- Use simple present tense for descriptions. Use imperative mood for instructions.
- Do not use future tense unless describing an unavoidable future state.
- Do not hide actions in nouns. (Example: "Analyze the data." NOT "Perform an analysis of the data.")
- Avoid "-ing" words when a simple noun or infinitive verb works better.
- Place warnings and cautions before the step that causes the risk.
- Start conditional sentences with the condition. (Example: "If the file exists, delete it." NOT "Delete the file if it exists.")
- Write steps chronologically. One action per step.

Do not open the output with conversational filler. (Example: do not start with "Here is the rewritten text".) Output the STE-compliant documentation directly.

### 2. Generate Mode

Generate new STE-compliant text from scratch for a feature or code block.

Apply all STE rules listed in Rewrite Mode above. Produce documentation that is lean, instructional, and immediately actionable.

### 3. Lint Mode

Review existing text and provide a bulleted list of STE violations with suggested corrections.

For each violation, include:

- the exact offending text
- the STE rule it violates
- a corrected version that complies with the rule

## Inputs

- **Mode**: rewrite, generate, or lint
- **Input text**: the text to process (for rewrite and lint modes)
- **Feature or subject**: the feature, API, or code module to document (for generate mode)
- **Context**: optional references such as existing docs, code files, or configuration

## Procedure

1. **Determine mode**
   - Clarify if the user wants rewrite, generate, or lint.
   - If not specified, default to lint and list violations.

2. **Apply STE rules**
   - Enforce all structural, lexical, and grammatical constraints.
   - Verify sentence length, paragraph length, and punctuation rules.
   - Check vocabulary for synonym rotation, phrasal verbs, and marketing fluff.
   - Verify active voice, present tense, and imperative mood usage.
   - Check noun cluster length (max 3).

3. **Format procedural steps**
   - Write steps in chronological order.
   - One action per step.
   - Place warnings before the step that causes the risk.
   - Start conditional statements with the condition.

4. **Remove conversational filler**
   - Strip all introductions, summaries, and meta-commentary.
   - Output only the STE-compliant documentation.

5. **Verify output**
   - Confirm all STE rules are applied.
   - Confirm the output is self-contained and actionable.
   - Confirm no hedging, modals, or marketing language remains.

## Decision Points

- If the input text has severe violations (>20 per page), rewrite the full document instead of providing a lint list.
- If the input text is already mostly STE-compliant, provide only the remaining violations.
- If the user does not specify a mode, default to lint mode.
- If the input is code or configuration, generate prose documentation that describes it in STE-compliant language.

## Completion Criteria

STE writing work is complete only when all are true:

- all applicable STE rules have been applied to the output
- no hedging, marketing fluff, or passive voice remains
- sentence and paragraph lengths comply with limits
- vocabulary is consistent (no synonym rotation)
- procedures are chronological with one action per step
- warnings precede the steps they protect
- conversational filler has been removed from the output

## Quality Bar

- Every output must be directly usable without editing.
- Prefer shorter sentences over longer ones.
- Prefer concrete terms over abstract ones.
- Do not preserve the original author's style if it violates STE rules.

## Example Prompts

- `/ste-technical-writing rewrite <paste text>`
- `/ste-technical-writing generate ValidateUser method in AuthService.cs`
- `/ste-technical-writing lint <paste documentation text>`
- `/ste-technical-writing rewrite the rendering pipeline section in DesignDoc.md`
