namespace Test.Shared
{
    /// <summary>
    /// Mutable holder used to observe side effects from inside case bodies.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    public sealed class StrongBox<T>
    {
        /// <summary>
        /// The held value.
        /// </summary>
        public T Value { get; set; } = default!;
    }
}
