using System.Diagnostics.CodeAnalysis;
using UniversityRegistration.Priority;

// Learning experiments only: excluded from the web project's compilation.
internal static class FactoryExperiments
{
    static void Check(bool value, string label)
    {
        if (!value) throw new BehaviorFailure("Factory experiment: " + label);
    }

    public static void Run(bool probe = false)
    {
        var input = new PriorityInput(225, 240);
        foreach (var (policy, expected) in new[] { ("graduation", 100), ("completed-credits", 225) })
        {
            // Direct condition demonstrates the nullable contract without suppressions.
            if (SimplePriorityFactory.TryCreate(policy, out var selected))
                Check(selected.Calculate(input) == expected, $"simple {policy}: expected {expected}");
            else throw new BehaviorFailure("Supported policy rejected");
            Check(SimplePriorityFactory.TryCreate(policy, out var another) && !ReferenceEquals(selected, another), "fresh instance contract");
        }
        foreach (var policy in new string?[] { null, "", "unknown", " GRADUATION " })
            Check(!SimplePriorityFactory.TryCreate(policy, out var rejected) && rejected is null, "unsupported/non-normalized input returns false/null");
        Console.WriteLine("PASS: experiment Simple Factory selection, false/null and fresh instances");

        PreviewCreator[] creators = [new GraduationPreviewCreator(), new CompletedCreditsPreviewCreator()];
        Check(creators[0].Preview(input) == 100 && creators[1].Preview(input) == 225, "Factory Method shared flow scores 100/225");
        Console.WriteLine("PASS: experiment Factory Method common Preview with overridden creation: 100/225");

        IPriorityFamilyFactory[] families = [new GraduationFamilyFactory(), new CompletedCreditsFamilyFactory()];
        var expectedScores = new[] { 100, 225 };
        var expectedExplanations = new[] { "Mezuniyet için kalan kredi: 15", "Tamamlanan kredi: 225" };
        for (var i = 0; i < families.Length; i++)
        {
            var strategy = families[i].CreateStrategy();
            var explanation = families[i].CreateExplanation();
            Check(strategy.Calculate(input) == expectedScores[i] && explanation.Explain(input) == expectedExplanations[i], "Abstract Factory score/explanation family coherence");
        }
        Check(new GraduationFamilyFactory().CreateExplanation().Explain(new(250,240)) == "Mezuniyet için kalan kredi: 0", "remaining credits clamped at zero");
        // Guard against accidentally shipping the learning types in the production assembly.
        Check(!typeof(PriorityInput).Assembly.GetTypes().Any(t => t.Name == nameof(FactoryExperiments)), "experiments excluded from web assembly");
        if (probe) Check(false, "controlled failure propagation probe");
        Console.WriteLine("PASS: experiment Abstract Factory coherent product families; experiments absent from web assembly");
    }

    // Input is already normalized by the caller. Null/unknown => false and null.
    // Each successful call supplies a fresh, stateless, non-disposable strategy.
    internal static class SimplePriorityFactory
    {
        public static bool TryCreate(string? normalizedPolicy, [NotNullWhen(true)] out IRegistrationPriorityStrategy? strategy)
        {
            strategy = normalizedPolicy switch
            {
                "graduation" => new GraduationPriorityStrategy(),
                "completed-credits" => new CompletedCreditsPriorityStrategy(),
                _ => null
            };
            return strategy is not null;
        }
    }

    internal abstract class PreviewCreator
    {
        public int Preview(PriorityInput input) => CreateStrategy().Calculate(input);
        protected abstract IRegistrationPriorityStrategy CreateStrategy();
    }
    internal sealed class GraduationPreviewCreator : PreviewCreator
    {
        protected override IRegistrationPriorityStrategy CreateStrategy() => new GraduationPriorityStrategy();
    }
    internal sealed class CompletedCreditsPreviewCreator : PreviewCreator
    {
        protected override IRegistrationPriorityStrategy CreateStrategy() => new CompletedCreditsPriorityStrategy();
    }

    internal interface IPriorityExplanation { string Explain(PriorityInput input); }
    internal interface IPriorityFamilyFactory
    {
        IRegistrationPriorityStrategy CreateStrategy();
        IPriorityExplanation CreateExplanation();
    }
    internal sealed class GraduationExplanation : IPriorityExplanation
    {
        public string Explain(PriorityInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            return $"Mezuniyet için kalan kredi: {Math.Max(0, input.RequiredCredits - input.CompletedCredits)}";
        }
    }
    internal sealed class CompletedCreditsExplanation : IPriorityExplanation
    {
        public string Explain(PriorityInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            return $"Tamamlanan kredi: {input.CompletedCredits}";
        }
    }
    internal sealed class GraduationFamilyFactory : IPriorityFamilyFactory
    {
        public IRegistrationPriorityStrategy CreateStrategy() => new GraduationPriorityStrategy();
        public IPriorityExplanation CreateExplanation() => new GraduationExplanation();
    }
    internal sealed class CompletedCreditsFamilyFactory : IPriorityFamilyFactory
    {
        public IRegistrationPriorityStrategy CreateStrategy() => new CompletedCreditsPriorityStrategy();
        public IPriorityExplanation CreateExplanation() => new CompletedCreditsExplanation();
    }
}
