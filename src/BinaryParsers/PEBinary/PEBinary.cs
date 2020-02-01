﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PEBinary : BinaryBase
    {
        private Lazy<Pdb> pdb;
        private readonly string symbolPath;
        private readonly string localSymbolDirectories;

        public PEBinary(Uri uri, string symbolPath = null, string localSymbolDirectories = null) : base(uri)
        {
            this.PE = new PE(this.TargetUri.LocalPath);
            this.IsManagedAssembly = this.PE.IsManaged;
            this.LoadException = this.PE.LoadException;
            this.Valid = this.PE.IsPEFile;
            this.symbolPath = symbolPath;
            this.localSymbolDirectories = localSymbolDirectories;

            this.pdb = new Lazy<Pdb>(this.LoadPdb, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private Pdb LoadPdb()
        {
            // We should never be required to load a PDB for a managed assembly that does
            // not incorporate native code, as no managed-relevant rule currently crawls
            // PDBs for its analysis.
            Debug.Assert(!this.PE.IsManaged || this.PE.IsMixedMode);

            Pdb pdb = null;
            try
            {
                pdb = new Pdb(this.PE.FileName, this.symbolPath, this.localSymbolDirectories);
            }
            catch (PdbParseException ex)
            {
                this.PdbParseException = ex;
            }

            if (pdb != null && pdb.IsStripped)
            {
                this.StrippedPdb = pdb;
                pdb = null;
                this.PdbParseException = new PdbParseException(BinaryParsersResources.PdbStripped);
            }
            return pdb;
        }

        public PdbParseException PdbParseException { get; internal set; }

        public bool IsManagedAssembly { get; internal set; }

        public PE PE { get; private set; }

        public Pdb Pdb => this.pdb?.Value;

        public Pdb StrippedPdb { get; private set; }

        public void DisposePortableExecutableData()
        {
            if (this.pdb != null &&
                this.pdb.IsValueCreated &&
                this.pdb.Value != null)
            {
                this.pdb.Value.Dispose();
            }
            this.pdb = null;

            if (this.PE != null)
            {
                this.PE.Dispose();
                this.PE = null;
            }
        }

        public override void Dispose()
        {
            this.DisposePortableExecutableData();
        }

        public static bool CanLoadBinary(Uri uri)
        {
            try
            {
                using (FileStream fs = File.OpenRead(Path.GetFullPath(uri.LocalPath)))
                {
                    return PE.CheckPEMagicBytes(fs);
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
