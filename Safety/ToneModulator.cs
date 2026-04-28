public static class ToneModulator
{
    public static (string Message, bool ShouldProcess) Modulate(
        string originalMessage, SafetyResult safety)
    {
        if (!safety.ShouldSuspend)
            return (originalMessage, true);

        if (safety.IsFrustrated)
        {
            var prefix =
                "[TONE:EMPATHETIC] [ESCALATE_IF_UNRESOLVED] " +
                "[CONTEXT: Customer appears frustrated. Acknowledge feelings before proceeding. " +
                "Do not comment on language used.] ";

            return (prefix + originalMessage, true);
        }

        var suspendPrefix =
            "[TONE:EMPATHETIC] [TRANSACTION_SUSPENDED] " +
            $"[CONTEXT: Message flagged — severity {safety.MaxSeverity} on {safety.Category}. " +
            "Do NOT invoke any tools. Calmly de-escalate and offer human agent escalation.] ";

        return (suspendPrefix + originalMessage, false);
    }
}