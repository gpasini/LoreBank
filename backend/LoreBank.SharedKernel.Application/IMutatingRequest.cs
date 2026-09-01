namespace LoreBank.SharedKernel.Application;

// Porté par tout ce qui mute, jamais par une query : c'est ce marqueur, et lui
// seul, qui décide de ce que le TransactionBehavior enveloppe.
public interface IMutatingRequest;
