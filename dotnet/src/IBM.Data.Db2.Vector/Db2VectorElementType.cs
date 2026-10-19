// Copyright (c) IBM Corporation. All rights reserved.

namespace IBM.Data.Db2.Vector;

/// <summary>
/// The coordinate types of the Db2 <c>VECTOR</c> data type.
/// </summary>
#pragma warning disable CA1720 // Identifier contains type name: the members mirror the Db2 keywords FLOAT32 and INT8.
public enum Db2VectorElementType
{
    /// <summary>32-bit floating point coordinates (<c>FLOAT32</c>).</summary>
    Float32,

    /// <summary>8-bit integer coordinates (<c>INT8</c>).</summary>
    Int8,
}
