using System;
using System.Collections.Generic;
using System.IO;
using Logistix.ModPlayer;
using Logistix.Util;

namespace Logistix.SerDe
{
    /// <summary>Try and isolate failures from affecting other parts by building a map of offsets similar to how the plugin does </summary>
    public abstract class TocBasedSerDe : ISerDe
    {
        protected bool skipWritingVersion = false;

        public abstract List<InstanceSerializer> GetSections();

        /// <summary>
        /// Re-register a fresh local player before a local save import so no state carries over
        /// from the previous slot. Overridden to a no-op by <see cref="SerDeRemoteUserState"/>,
        /// which deserialises a remote player (including re-entrantly, from the PSC section of a
        /// local import) and must never touch the local registry.
        /// </summary>
        protected virtual void PrepareForImport()
        {
            PlogPlayerRegistry.ClearLocal();
            PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());
        }

        public void Import(BinaryReader r)
        {
            PrepareForImport();
            // Build the section list once and reuse it for the whole import: GetSections() constructs
            // fresh instances (e.g. RecycleWindowPersistence) on each call, so re-invoking it per
            // section would import into throwaway objects nobody keeps a reference to.
            var sections = GetSections();
            var tableOfContents = TableOfContents.Import(r);
            try
            {
                foreach (var contentsItem in tableOfContents.GetItems())
                {
                    // create a stream for each section and process them one by one
                    InstanceSerializer instance = null;
                    try
                    {
                        Type type = GetTypeFromSectionName(sections, contentsItem.sectionName);
                        Log.Debug($"starting to read {type} at {contentsItem.startIndexAbsolute}, {contentsItem.length}");
                        using var memoryStream = new MemoryStream(r.ReadBytes(contentsItem.length));
                        using var sectionReader = new BinaryReader(memoryStream);
                        try
                        {
                            var section = sectionReader.ReadString();
                            if (section != contentsItem.sectionName)
                            {
                                throw new InvalidDataException($"section name was: '{section}', expected {contentsItem.sectionName}");
                            }

                            instance = GetInstanceFromType(sections, type);
                            instance.ImportData(sectionReader);
                            Log.Debug($"successful read in section: {type} {instance.SummarizeState()}");
                        }
                        catch (Exception e)
                        {
                            Log.Warn($"Import failure for section: {contentsItem.sectionName}. Falling back to init \r\b{e.Message}{e.StackTrace}");
                            if (instance != null)
                                instance.InitOnLoad();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"failed to import contents item: {contentsItem.sectionName}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"failed to read data section");
            }
        }

        private static Type GetTypeFromSectionName(List<InstanceSerializer> sections, string sectionName)
        {
            var results = sections.FindAll(s => s.GetExportSectionId() == sectionName);
            if (results.Count == 0 || results.Count > 1)
            {
                throw new InvalidDataException($"Expected only 1 type with section name: {sectionName} found {results.Count}");
            }

            return results[0].GetType();
        }

        private static InstanceSerializer GetInstanceFromType(List<InstanceSerializer> sections, Type type)
        {
            var results = sections.FindAll(s => type == s.GetType());
            if (results.Count is 0 or > 1)
            {
                throw new InvalidDataException($"Expected only 1 instance in list with type: {type} found {results.Count}");
            }

            return results[0];
        }

        /// <summary>
        /// version, table of contents, datalen, data
        /// </summary>
        /// <param name="w"></param>
        public void Export(BinaryWriter w)
        {
            if (!skipWritingVersion)
                w.Write(GetVersion());
            var tableOfContents = new TableOfContents();

            var partList = new List<byte[]>();
            try
            {
                foreach (var section in GetSections())
                {
                    var sectionData = WriteType(section);
                    partList.Add(sectionData);
                    tableOfContents.AddItem(sectionData, section.GetExportSectionId());
                }
            }
            catch (Exception e)
            {
                Log.Warn($"got exception while writing parts to mem: {e.Message}");
            }

            // write TOC next
            tableOfContents.Export(w);

            foreach (var part in partList)
            {
                w.Write(part);
            }

            w.Flush();
        }

        protected abstract int GetVersion();

        private byte[] WriteType(InstanceSerializer instance)
        {
            using var memoryStream = new MemoryStream();
            try
            {
                using var writer = new BinaryWriter(memoryStream);
                writer.Write(instance.GetExportSectionId());
                try
                {
                    instance.ExportData(writer);
                    Log.Debug($"stored {instance.GetType().Name} {instance.GetExportSectionId()}");
                }
                catch (Exception e)
                {
                    Log.Warn($"Falling back to init for {instance.GetType()}. {e.Message} {e.StackTrace}");
                    instance.InitOnLoad();
                }
            }
            catch (Exception e)
            {
                Log.Warn($"problem while writing {instance.GetType()}. {e.Message}");
            }

            return memoryStream.ToArray();
        }

        internal class TableOfContentsItem
        {
            public string sectionName;
            public int startIndexAbsolute;
            public int length;

            public Type GetType(Dictionary<Type, string> typeNames)
            {
                foreach (var sect in typeNames)
                {
                    if (sect.Value == sectionName)
                    {
                        return sect.Key;
                    }
                }

                throw new InvalidDataException($"no type for section: {sectionName}");
            }
        }

        internal class TableOfContents
        {
            private readonly List<TableOfContentsItem> _items = new();

            public int DataSectionSize;

            public void AddItem(byte[] data, string sectionName)
            {
                _items.Add(new TableOfContentsItem
                {
                    sectionName = sectionName,
                    startIndexAbsolute = DataSectionSize,
                    length = data.Length,
                });
                DataSectionSize += data.Length;
            }

            // toc is: count, [(name, actualPos, length)]
            public void Export(BinaryWriter w)
            {
                w.Write(_items.Count);
                Log.Debug($"wrote {_items.Count} (begin of toc)");
                foreach (var item in _items)
                {
                    w.Write(item.sectionName);
                    w.Write(item.startIndexAbsolute);
                    w.Write(item.length);
                    Log.Debug($"wrote {item.sectionName}, {item.startIndexAbsolute}, {item.length} current toc item");
                }

                Log.Debug($"exported {_items.Count} sections");
                w.Flush();
            }

            public static TableOfContents Import(BinaryReader r)
            {
                var tableOfContents = new TableOfContents();
                var count = r.ReadInt32();
                Log.Debug($"importing {count} toc entries");
                for (int i = 0; i < count; i++)
                {
                    tableOfContents._items.Add(new TableOfContentsItem
                    {
                        sectionName = r.ReadString(),
                        startIndexAbsolute = r.ReadInt32(),
                        length = r.ReadInt32(),
                    });
                    // Log.Debug($"imported toc item: {tableOfContents._items[tableOfContents._items.Count - 1].sectionName}");
                }

                Log.Debug($"imported {tableOfContents._items.Count} items, last toc item: {tableOfContents._items[tableOfContents._items.Count - 1].sectionName}");

                return tableOfContents;
            }

            public List<TableOfContentsItem> GetItems()
            {
                return new List<TableOfContentsItem>(_items);
            }
        }
    }
}