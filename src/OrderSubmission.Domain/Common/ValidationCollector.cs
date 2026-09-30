namespace OrderSubmission.Domain.Common;

internal sealed class ValidationCollector
{
    private readonly Dictionary<string, List<string>> _failures = new(StringComparer.Ordinal);

    public bool HasFailures => _failures.Count > 0;

    public void Add(string field, Error error)
    {
        if (error is ValidationError nested)
        {
            foreach (var (nestedField, messages) in nested.Failures)
            {
                foreach (var message in messages)
                {
                    Append(Combine(field, nestedField), message);
                }
            }

            return;
        }

        Append(field, error.Description);
    }

    public T? Collect<T>(string field, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }

        Add(field, result.Error);
        return default;
    }

    public ValidationError ToError() =>
        new(_failures.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));

    private void Append(string field, string message)
    {
        if (!_failures.TryGetValue(field, out var messages))
        {
            messages = [];
            _failures[field] = messages;
        }

        messages.Add(message);
    }

    private static string Combine(string prefix, string field) =>
        string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";
}
