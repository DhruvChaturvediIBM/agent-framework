// Copyright (c) IBM Corporation. All rights reserved.

namespace IBM.Data.Db2.Vector;

/// <summary>
/// The distance metrics accepted by the Db2 <c>VECTOR_DISTANCE</c> scalar function.
/// </summary>
/// <remarks>
/// Every metric returns a distance: lower values mean more similar vectors. This includes <see cref="Dot"/>,
/// for which Db2 returns the negated dot product.
/// </remarks>
public enum Db2VectorDistanceMetric
{
    /// <summary>Euclidean (L2) distance.</summary>
    Euclidean,

    /// <summary>Squared Euclidean distance.</summary>
    EuclideanSquared,

    /// <summary>Cosine distance (1 - cosine similarity). Undefined for zero vectors.</summary>
    Cosine,

    /// <summary>Negative dot product.</summary>
    Dot,

    /// <summary>Hamming distance: the number of coordinates that differ.</summary>
    Hamming,

    /// <summary>Manhattan (L1) distance.</summary>
    Manhattan,
}
