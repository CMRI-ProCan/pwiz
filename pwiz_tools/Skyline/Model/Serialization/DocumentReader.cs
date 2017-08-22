﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    public class DocumentReader : DocumentSerializer
    {
        private readonly StringPool _stringPool = new StringPool();
        public DocumentFormat FormatVersion { get; private set; }
        public PeptideGroupDocNode[] Children { get; private set; }

        private readonly Dictionary<string, string> _uniqueSpecies = new Dictionary<string, string>();

        /// <summary>
        /// In older versions of Skyline we would handle ion notation by building it into the molecule, 
        /// so our current C12H5[M+2H] would have been C12H7 - this requires special handling on read
        /// </summary>
        public bool DocumentMayContainMoleculesWithEmbeddedIons { get { return FormatVersion <= DocumentFormat.VERSION_3_71; } }

        /// <summary>
        /// Avoids duplication of species strings
        /// </summary>
        public string GetUniqueSpecies(string species)
        {
            if (species == null)
                return null;
            string uniqueSpecies;
            if (!_uniqueSpecies.TryGetValue(species, out uniqueSpecies))
            {
                _uniqueSpecies.Add(species, species);
                uniqueSpecies = species;
            }
            return uniqueSpecies;
        }

        private PeptideChromInfo ReadPeptideChromInfo(XmlReader reader, ChromFileInfoId fileId)
        {
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            bool excludeFromCalibration = reader.GetBoolAttribute(ATTR.exclude_from_calibration);
            return new PeptideChromInfo(fileId, peakCountRatio, retentionTime, ImmutableList<PeptideLabelRatio>.EMPTY)
                .ChangeExcludeFromCalibration(excludeFromCalibration);
        }

        private SpectrumHeaderInfo ReadTransitionGroupLibInfo(XmlReader reader)
        {
            // Look for an appropriate deserialization helper for spectrum
            // header info on the current tag.
            var helpers = PeptideLibraries.SpectrumHeaderXmlHelpers;
            var helper = reader.FindHelper(helpers);
            if (helper != null)
            {
                var libInfo = helper.Deserialize(reader);
                return libInfo.ChangeLibraryName(_stringPool.GetString(libInfo.LibraryName));
            }

            return null;
        }

        private TransitionGroupChromInfo ReadTransitionGroupChromInfo(XmlReader reader, ChromFileInfoId fileId)
        {
            int optimizationStep = reader.GetIntAttribute(ATTR.step);
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            float? startTime = reader.GetNullableFloatAttribute(ATTR.start_time);
            float? endTime = reader.GetNullableFloatAttribute(ATTR.end_time);
            float? ccs = reader.GetNullableFloatAttribute(ATTR.ccs);
            float? driftTimeMS1 = reader.GetNullableFloatAttribute(ATTR.drift_time_ms1);
            float? driftTimeFragment = reader.GetNullableFloatAttribute(ATTR.drift_time_fragment);
            float? driftTimeWindow = reader.GetNullableFloatAttribute(ATTR.drift_time_window);
            float? fwhm = reader.GetNullableFloatAttribute(ATTR.fwhm);
            float? area = reader.GetNullableFloatAttribute(ATTR.area);
            float? backgroundArea = reader.GetNullableFloatAttribute(ATTR.background);
            float? height = reader.GetNullableFloatAttribute(ATTR.height);
            float? massError = reader.GetNullableFloatAttribute(ATTR.mass_error_ppm);
            int? truncated = reader.GetNullableIntAttribute(ATTR.truncated);
            PeakIdentification identified = reader.GetEnumAttribute(ATTR.identified, PeakIdentificationFastLookup.Dict,
                PeakIdentification.FALSE, XmlUtil.EnumCase.upper);
            float? libraryDotProduct = reader.GetNullableFloatAttribute(ATTR.library_dotp);
            float? isotopeDotProduct = reader.GetNullableFloatAttribute(ATTR.isotope_dotp);
            float? qvalue = reader.GetNullableFloatAttribute(ATTR.qvalue);
            float? zscore = reader.GetNullableFloatAttribute(ATTR.zscore);
            var annotations = Annotations.EMPTY;
            if (!reader.IsEmptyElement)
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader, _stringPool);
                // Convert q value and mProphet score annotations to numbers for the ChromInfo object
                annotations = ReadAndRemoveScoreAnnotation(annotations, MProphetResultsHandler.AnnotationName, ref qvalue);
                annotations = ReadAndRemoveScoreAnnotation(annotations, MProphetResultsHandler.MAnnotationName, ref zscore);
            }
            // Ignore userSet during load, since all values are still calculated
            // from the child transitions.  Otherwise inconsistency is possible.
//            bool userSet = reader.GetBoolAttribute(ATTR.user_set);
            const UserSet userSet = UserSet.FALSE;
            int countRatios = Settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
            return new TransitionGroupChromInfo(fileId,
                optimizationStep,
                peakCountRatio,
                retentionTime,
                startTime,
                endTime,
                TransitionGroupDriftTimeInfo.GetTransitionGroupIonMobilityInfo(ccs, driftTimeMS1, driftTimeFragment, driftTimeWindow),
                fwhm,
                area, null, null, // Ms1 and Fragment values calculated later
                backgroundArea, null, null, // Ms1 and Fragment values calculated later
                height,
                TransitionGroupChromInfo.GetEmptyRatios(countRatios),
                massError,
                truncated,
                identified,
                libraryDotProduct,
                isotopeDotProduct,
                qvalue,
                zscore,
                annotations,
                userSet);
        }

        private static Annotations ReadAndRemoveScoreAnnotation(Annotations annotations, string annotationName, ref float? annotationValue)
        {
            string annotationText = annotations.GetAnnotation(annotationName);
            if (String.IsNullOrEmpty(annotationText))
                return annotations;
            double scoreValue;
            if (Double.TryParse(annotationText, out scoreValue))
                annotationValue = (float) scoreValue;
            return annotations.RemoveAnnotation(annotationName);
        }

        /// <summary>
        /// Reads annotations without ensuring that they use a single unique key string. This
        /// is currently only used for <see cref="ChromatogramSet"/>, because it is difficult to
        /// get it to use the version with a non-null context and the possible level of repetition
        /// is much smaller than with the document nodes and results objects.
        /// </summary>
        private static Annotations ReadAnnotations(XmlReader reader, StringPool stringPool)
        {
            string note = null;
            int color = 0;
            var annotations = new Dictionary<string, string>();
            
            if (reader.IsStartElement(EL.note))
            {
                color = reader.GetIntAttribute(ATTR.category);
                note = reader.ReadElementString();
            }
            while (reader.IsStartElement(EL.annotation))
            {
                string name = reader.GetAttribute(ATTR.name);
                if (name == null)
                    throw new InvalidDataException(Resources.SrmDocument_ReadAnnotations_Annotation_found_without_name);
                if (stringPool != null)
                    name = stringPool.GetString(name);
                annotations[name] = reader.ReadElementString();
            }

            return note != null || annotations.Count > 0
                ? new Annotations(note, annotations, color)
                : Annotations.EMPTY;
        }

        public static Annotations ReadAnnotations(XmlReader reader)
        {
            return ReadAnnotations(reader, null);
        }

        /// <summary>
        /// Helper class for reading information from a transition element into
        /// memory for use in both <see cref="Transition"/> and <see cref="TransitionGroup"/>.
        /// 
        /// This class exists to share code between <see cref="ReadTransitionXml"/>
        /// and <see cref="ReadUngroupedTransitionListXml"/>.
        /// </summary>
        private class TransitionInfo
        {
            private readonly DocumentReader _documentReader;
            public TransitionInfo(DocumentReader documentReader)
            {
                _documentReader = documentReader;
            }
            public SrmSettings Settings { get { return _documentReader.Settings; } }
            public IonType IonType { get; private set; }
            public int Ordinal { get; private set; }
            public int MassIndex { get; private set; }
            public Adduct PrecursorAdduct { get; private set; }
            public Adduct ProductAdduct { get; private set; }
            public int? DecoyMassShift { get; private set; }
            public TransitionLosses Losses { get; private set; }
            public Annotations Annotations { get; private set; }
            public TransitionLibInfo LibInfo { get; private set; }
            public Results<TransitionChromInfo> Results { get; private set; }
            public MeasuredIon MeasuredIon { get; private set; }
            public bool Quantitative { get; private set; }

            public void ReadXml(XmlReader reader)
            {
                ReadXmlAttributes(reader);
                double? ignoredDeclaredMz;
                ReadXmlElements(reader, out ignoredDeclaredMz);
            }

            public void ReadXmlAttributes(XmlReader reader)
            {
                // Accept uppercase and lowercase for backward compatibility with v0.1
                IonType = reader.GetEnumAttribute(ATTR.fragment_type, IonType.y, XmlUtil.EnumCase.lower);
                Ordinal = reader.GetIntAttribute(ATTR.fragment_ordinal);
                MassIndex = reader.GetIntAttribute(ATTR.mass_index);
                // NOTE: PrecursorCharge is used only in TransitionInfo.ReadUngroupedTransitionListXml()
                //       to support v0.1 document format
                PrecursorAdduct = Adduct.FromStringAssumeProtonated(reader.GetAttribute(ATTR.precursor_charge));
                ProductAdduct = Adduct.FromStringAssumeProtonated(reader.GetAttribute(ATTR.product_charge));
                DecoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
                Quantitative = reader.GetBoolAttribute(ATTR.quantitative, true);
                string measuredIonName = reader.GetAttribute(ATTR.measured_ion_name);
                if (measuredIonName != null)
                {
                    MeasuredIon = Settings.TransitionSettings.Filter.MeasuredIons.SingleOrDefault(
                        i => i.Name.Equals(measuredIonName));
                    if (MeasuredIon == null)
                        throw new InvalidDataException(String.Format(Resources.TransitionInfo_ReadXmlAttributes_The_reporter_ion__0__was_not_found_in_the_transition_filter_settings_, measuredIonName));
                    IonType = IonType.custom;
                }
            }

            public void ReadXmlElements(XmlReader reader, out double? declaredProductMz)
            {
                declaredProductMz = null;
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();
                    Annotations = ReadAnnotations(reader, _documentReader._stringPool); // This is reliably first in all versions
                    while (true)
                    {  // The order of these elements may depend on the version of the file being read
                        if (reader.IsStartElement(EL.losses))
                            Losses = ReadTransitionLosses(reader);
                        else if (reader.IsStartElement(EL.transition_lib_info))
                            LibInfo = ReadTransitionLibInfo(reader);
                        else if (reader.IsStartElement(EL.transition_results) || reader.IsStartElement(EL.results_data))
                            Results = ReadTransitionResults(reader);
                        // Read and discard informational elements.  These values are always
                        // calculated from the settings to ensure consistency.
                        // Note that we do use product_mz to disambiguate some older mass-only small molecule documents.
                        else if (reader.IsStartElement(EL.precursor_mz))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.product_mz))
                            declaredProductMz = reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.collision_energy))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.declustering_potential))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.start_rt))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.stop_rt))
                            reader.ReadElementContentAsDoubleInvariant();
                        else
                            break;
                    }
                    reader.ReadEndElement();
                }
            }

            private TransitionLosses ReadTransitionLosses(XmlReader reader)
            {
                if (reader.IsStartElement(EL.losses))
                {
                    var staticMods = Settings.PeptideSettings.Modifications.StaticModifications;
                    MassType massType = Settings.TransitionSettings.Prediction.FragmentMassType;

                    reader.ReadStartElement();
                    var listLosses = new List<TransitionLoss>();
                    while (reader.IsStartElement(EL.neutral_loss))
                    {
                        string nameMod = reader.GetAttribute(ATTR.modification_name);
                        if (String.IsNullOrEmpty(nameMod))
                            listLosses.Add(new TransitionLoss(null, FragmentLoss.Deserialize(reader), massType));
                        else
                        {
                            int indexLoss = reader.GetIntAttribute(ATTR.loss_index);
                            int indexMod = staticMods.IndexOf(mod => Equals(nameMod, mod.Name));
                            if (indexMod == -1)
                            {
                                throw new InvalidDataException(
                                    String.Format(Resources.TransitionInfo_ReadTransitionLosses_No_modification_named__0__was_found_in_this_document,
                                        nameMod));
                            }
                            StaticMod modLoss = staticMods[indexMod];
                            if (!modLoss.HasLoss || indexLoss >= modLoss.Losses.Count)
                            {
                                throw new InvalidDataException(
                                    String.Format(Resources.TransitionInfo_ReadTransitionLosses_Invalid_loss_index__0__for_modification__1__,
                                        indexLoss, nameMod));
                            }
                            listLosses.Add(new TransitionLoss(modLoss, modLoss.Losses[indexLoss], massType));
                        }
                        reader.Read();
                    }
                    reader.ReadEndElement();

                    return new TransitionLosses(listLosses, massType);
                }
                return null;
            }

            private static TransitionLibInfo ReadTransitionLibInfo(XmlReader reader)
            {
                if (reader.IsStartElement(EL.transition_lib_info))
                {
                    var libInfo = new TransitionLibInfo(reader.GetIntAttribute(ATTR.rank),
                        reader.GetFloatAttribute(ATTR.intensity));
                    reader.ReadStartElement();
                    return libInfo;
                }
                return null;
            }

            private Results<TransitionChromInfo> ReadTransitionResults(XmlReader reader)
            {
                if (reader.IsStartElement(EL.results_data))
                {
                    string strContent = reader.ReadElementString();
                    byte[] data = Convert.FromBase64String(strContent);
                    var protoTransitionResults = new SkylineDocumentProto.Types.TransitionResults();
                    protoTransitionResults.MergeFrom(data);
                    return TransitionChromInfo.FromProtoTransitionResults(_documentReader._stringPool, Settings, protoTransitionResults);
                }
                if (reader.IsStartElement(EL.transition_results))
                    return _documentReader.ReadResults(reader, EL.transition_peak, ReadTransitionPeak);
                return null;
            }

            private TransitionChromInfo ReadTransitionPeak(XmlReader reader, ChromFileInfoId fileId)
            {
                int optimizationStep = reader.GetIntAttribute(ATTR.step);
                float? massError = reader.GetNullableFloatAttribute(ATTR.mass_error_ppm);
                float retentionTime = reader.GetFloatAttribute(ATTR.retention_time);
                float startRetentionTime = reader.GetFloatAttribute(ATTR.start_time);
                float endRetentionTime = reader.GetFloatAttribute(ATTR.end_time);
                // Protect against negative areas, since they can cause real problems
                // for ratio calculations.
                float area = Math.Max(0, reader.GetFloatAttribute(ATTR.area));
                float backgroundArea = Math.Max(0, reader.GetFloatAttribute(ATTR.background));
                float height = reader.GetFloatAttribute(ATTR.height);
                float fwhm = reader.GetFloatAttribute(ATTR.fwhm);
                // Strange issue where fwhm got set to NaN
                if (Single.IsNaN(fwhm))
                    fwhm = 0;
                bool fwhmDegenerate = reader.GetBoolAttribute(ATTR.fwhm_degenerate);
                short rank = (short) reader.GetIntAttribute(ATTR.rank);
                short rankByLevel = (short) reader.GetIntAttribute(ATTR.rank_by_level, rank);
                bool? truncated = reader.GetNullableBoolAttribute(ATTR.truncated);
                short? pointsAcross = (short?) reader.GetNullableIntAttribute(ATTR.points_across);
                var identified = reader.GetEnumAttribute(ATTR.identified, PeakIdentificationFastLookup.Dict,
                    PeakIdentification.FALSE, XmlUtil.EnumCase.upper);
                UserSet userSet = reader.GetEnumAttribute(ATTR.user_set, UserSetFastLookup.Dict,
                    UserSet.FALSE, XmlUtil.EnumCase.upper);
                double? driftTime = reader.GetNullableDoubleAttribute(ATTR.drift_time);
                double? driftTimeWindow = reader.GetNullableDoubleAttribute(ATTR.drift_time_window);
                var annotations = Annotations.EMPTY;
                if (!reader.IsEmptyElement)
                {
                    reader.ReadStartElement();
                    annotations = ReadAnnotations(reader, _documentReader._stringPool);
                }
                int countRatios = _documentReader.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
                return new TransitionChromInfo(fileId,
                    optimizationStep,
                    massError,
                    retentionTime,
                    startRetentionTime,
                    endRetentionTime, 
                    DriftTimeFilter.GetDriftTimeFilter(driftTime, driftTimeWindow, null), 
                    area,
                    backgroundArea,
                    height,
                    fwhm,
                    fwhmDegenerate,
                    truncated,
                    pointsAcross,
                    identified,
                    rank,
                    rankByLevel,
                    TransitionChromInfo.GetEmptyRatios(countRatios),
                    annotations,
                    userSet);
            }
        }

        private Results<TItem> ReadResults<TItem>(XmlReader reader, string start,
            Func<XmlReader, ChromFileInfoId, TItem> readInfo)
            where TItem : ChromInfo
        {
            // If the results element is empty, then there are no results to read.
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return null;
            }

            MeasuredResults results = Settings.MeasuredResults;
            if (results == null)
                throw new InvalidDataException(Resources.SrmDocument_ReadResults_No_results_information_found_in_the_document_settings);

            reader.ReadStartElement();
            var arrayListChromInfos = new List<TItem>[results.Chromatograms.Count];
            ChromatogramSet chromatogramSet = null;
            int index = -1;
            while (reader.IsStartElement(start))
            {
                string name = reader.GetAttribute(ATTR.replicate);
                if (chromatogramSet == null || !Equals(name, chromatogramSet.Name))
                {
                    if (!results.TryGetChromatogramSet(name, out chromatogramSet, out index))
                        throw new InvalidDataException(String.Format(Resources.SrmDocument_ReadResults_No_replicate_named__0__found_in_measured_results, name));
                }
                string fileId = reader.GetAttribute(ATTR.file);
                var fileInfoId = (fileId != null
                    ? chromatogramSet.FindFileById(fileId)
                    : chromatogramSet.MSDataFileInfos[0].FileId);
                if (fileInfoId == null)
                    throw new InvalidDataException(String.Format(Resources.SrmDocument_ReadResults_No_file_with_id__0__found_in_the_replicate__1__, fileId, name));

                TItem chromInfo = readInfo(reader, fileInfoId);
                // Consume the tag
                reader.Read();

                if (!ReferenceEquals(chromInfo, default(TItem)))
                {
                    if (arrayListChromInfos[index] == null)
                        arrayListChromInfos[index] = new List<TItem>();
                    // Deal with cache corruption issue where the same results info could
                    // get written multiple times for the same precursor.
                    var listChromInfos = arrayListChromInfos[index];
                    if (listChromInfos.Count == 0 || !Equals(chromInfo, listChromInfos[listChromInfos.Count - 1]))
                        arrayListChromInfos[index].Add(chromInfo);
                }
            }
            reader.ReadEndElement();

            var arrayChromInfoLists = new ChromInfoList<TItem>[arrayListChromInfos.Length];
            for (int i = 0; i < arrayListChromInfos.Length; i++)
            {
                if (arrayListChromInfos[i] != null)
                    arrayChromInfoLists[i] = new ChromInfoList<TItem>(arrayListChromInfos[i]);
            }
            return new Results<TItem>(arrayChromInfoLists);
        }

        /// <summary>
        /// Deserializes document from XML.
        /// </summary>
        /// <param name="reader">The reader positioned at the document start tag</param>
        public void ReadXml(XmlReader reader)
        {
            double formatVersionNumber = reader.GetDoubleAttribute(ATTR.format_version);
            if (formatVersionNumber == 0)
            {
                FormatVersion = DocumentFormat.VERSION_0_1;
            }
            else
            {
                FormatVersion = new DocumentFormat(formatVersionNumber);
                if (FormatVersion.CompareTo(DocumentFormat.CURRENT) > 0)
                {
// Resharper disable ImpureMethodCallOnReadonlyValueField
                    throw new VersionNewerException(
                        string.Format(Resources.SrmDocument_ReadXml_The_document_format_version__0__is_newer_than_the_version__1__supported_by__2__,
                            formatVersionNumber, DocumentFormat.CURRENT.AsDouble(), Install.ProgramNameAndVersion));
// Resharper enable ImpureMethodCallOnReadonlyValueField
                }
            }

            reader.ReadStartElement();  // Start document element

            Settings = reader.DeserializeElement<SrmSettings>() ?? SrmSettingsList.GetDefault();

            if (reader.IsStartElement())
            {
                // Support v0.1 naming
                if (!reader.IsStartElement(EL.selected_proteins))
                    Children = ReadPeptideGroupListXml(reader);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement();
                    Children = ReadPeptideGroupListXml(reader);
                    reader.ReadEndElement();
                }
            }

            reader.ReadEndElement();    // End document element
            if (Children == null)
                Children = new PeptideGroupDocNode[0];
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at the start of the list.
        /// </summary>
        /// <param name="reader">The reader positioned on the element of the first node</param>
        /// <returns>An array of <see cref="PeptideGroupDocNode"/> objects for
        ///         inclusion in a <see cref="SrmDocument"/> child list</returns>
        private PeptideGroupDocNode[] ReadPeptideGroupListXml(XmlReader reader)
        {
            var list = new List<PeptideGroupDocNode>();
            while (reader.IsStartElement(EL.protein) || reader.IsStartElement(EL.peptide_list))
            {
                if (reader.IsStartElement(EL.protein))
                    list.Add(ReadProteinXml(reader));
                else
                    list.Add(ReadPeptideGroupXml(reader));
            }
            return list.ToArray();
        }

        private ProteinMetadata ReadProteinMetadataXML(XmlReader reader, bool labelNameAndDescription)
        {
            var labelPrefix = labelNameAndDescription ? "label_" : string.Empty; // Not L10N
            return new ProteinMetadata(
                reader.GetAttribute(labelPrefix + ATTR.name),
                reader.GetAttribute(labelPrefix + ATTR.description),
                reader.GetAttribute(ATTR.preferred_name),
                reader.GetAttribute(ATTR.accession),
                reader.GetAttribute(ATTR.gene),
                GetUniqueSpecies(reader.GetAttribute(ATTR.species)),
                reader.GetAttribute(ATTR.websearch_status));
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at a &lt;protein&gt; tag.
        /// 
        /// In order to support the v0.1 format, the returned node may represent
        /// either a FASTA sequence or a peptide list.
        /// </summary>
        /// <param name="reader">The reader positioned at a protein tag</param>
        /// <returns>A new <see cref="PeptideGroupDocNode"/></returns>
        private PeptideGroupDocNode ReadProteinXml(XmlReader reader)
        {
            string name = reader.GetAttribute(ATTR.name);
            string description = reader.GetAttribute(ATTR.description);
            bool peptideList = reader.GetBoolAttribute(ATTR.peptide_list);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            var labelProteinMetadata = ReadProteinMetadataXML(reader, true);  // read label_name, label_description, and species, gene etc if any

            reader.ReadStartElement();

            var annotations = ReadAnnotations(reader, _stringPool);

            ProteinMetadata[] alternatives;
            if (!reader.IsStartElement(EL.alternatives) || reader.IsEmptyElement)
                alternatives = new ProteinMetadata[0];
            else
            {
                reader.ReadStartElement();
                alternatives = ReadAltProteinListXml(reader);
                reader.ReadEndElement();
            }

            reader.ReadStartElement(EL.sequence);
            string sequence = DecodeProteinSequence(reader.ReadContentAsString());
            reader.ReadEndElement();

            // Support v0.1 documents, where peptide lists were saved as proteins,
            // pre-v0.1 documents, which may not have identified peptide lists correctly.
            if (sequence.StartsWith("X") && sequence.EndsWith("X")) // Not L10N
                peptideList = true;

            // All v0.1 peptide lists should have a settable label
            if (peptideList)
            {
                labelProteinMetadata = labelProteinMetadata.ChangeName(name ?? string.Empty);
                labelProteinMetadata = labelProteinMetadata.ChangeDescription(description);
            }
            // Or any protein without a name attribute
            else if (name != null)
            {
                labelProteinMetadata = labelProteinMetadata.ChangeDescription(null);
            }

            PeptideGroup group;
            if (peptideList)
                group = new PeptideGroup();
            // If there is no name attribute, ignore all info from the FASTA header line,
            // since it should be user settable.
            else if (name == null)
                group = new FastaSequence(null, null, null, sequence);
            else
                group = new FastaSequence(name, description, alternatives, sequence);

            PeptideDocNode[] children = null;
            if (!reader.IsStartElement(EL.selected_peptides))
                children = ReadPeptideListXml(reader, group);
            else if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement(EL.selected_peptides);
                children = ReadPeptideListXml(reader, group);
                reader.ReadEndElement();
            }

            reader.ReadEndElement();

            return new PeptideGroupDocNode(group, annotations, labelProteinMetadata,
                children ?? new PeptideDocNode[0], autoManageChildren);
        }

        /// <summary>
        /// Deserializes an array of <see cref="ProteinMetadata"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <returns>A new array of <see cref="ProteinMetadata"/></returns>
        private ProteinMetadata[] ReadAltProteinListXml(XmlReader reader)
        {
            var list = new List<ProteinMetadata>();
            while (reader.IsStartElement(EL.alternative_protein))
            {
                var proteinMetaData = ReadProteinMetadataXML(reader, false);
                reader.Read();
                list.Add(proteinMetaData);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Decodes a FASTA sequence as stored in a XML document to one
        /// with all white space removed.
        /// </summary>
        /// <param name="sequence">The XML format sequence</param>
        /// <returns>The sequence suitible for use in a <see cref="FastaSequence"/></returns>
        private static string DecodeProteinSequence(IEnumerable<char> sequence)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char aa in sequence)
            {
                if (!char.IsWhiteSpace(aa))
                    sb.Append(aa);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> representing
        /// a peptide list from a <see cref="XmlReader"/> positioned at the
        /// start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a peptide group</param>
        /// <returns>A new <see cref="PeptideGroupDocNode"/></returns>
        private PeptideGroupDocNode ReadPeptideGroupXml(XmlReader reader)
        {
            ProteinMetadata proteinMetadata = ReadProteinMetadataXML(reader, true); // read label_name and label_description
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);

            PeptideGroup group = new PeptideGroup(isDecoy);

            Annotations annotations = Annotations.EMPTY;
            PeptideDocNode[] children = null;

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader, _stringPool);

                if (!reader.IsStartElement(EL.selected_peptides))
                    children = ReadPeptideListXml(reader, group);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement(EL.selected_peptides);
                    children = ReadPeptideListXml(reader, group);
                    reader.ReadEndElement();
                }

                reader.ReadEndElement();    // peptide_list
            }

            return new PeptideGroupDocNode(group, annotations, proteinMetadata,
                children ?? new PeptideDocNode[0], autoManageChildren);
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <returns>A new array of <see cref="PeptideDocNode"/></returns>
        private PeptideDocNode[] ReadPeptideListXml(XmlReader reader, PeptideGroup group)
        {
            var list = new List<PeptideDocNode>();
            while (reader.IsStartElement(EL.molecule) || reader.IsStartElement(EL.peptide))
            {
                list.Add(ReadPeptideXml(reader, group, reader.IsStartElement(EL.molecule)));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserialize any explictly set CE, DT, etc information from attributes
        /// </summary>
        private ExplicitTransitionGroupValues ReadExplicitTransitionValuesAttributes(XmlReader reader)
        {
            double? importedCollisionEnergy = reader.GetNullableDoubleAttribute(ATTR.explicit_collision_energy);
            double? importedDriftTimeMsec = reader.GetNullableDoubleAttribute(ATTR.explicit_drift_time_msec);
            double? importedDriftTimeHighEnergyOffsetMsec = reader.GetNullableDoubleAttribute(ATTR.explicit_drift_time_high_energy_offset_msec);
            double? importedCCS = reader.GetNullableDoubleAttribute(ATTR.explicit_ccs_sqa);
            double? importedSLens = reader.GetNullableDoubleAttribute(FormatVersion.CompareTo(DocumentFormat.VERSION_3_52) < 0 ? ATTR.s_lens_obsolete : ATTR.explicit_s_lens);
            double? importedConeVoltage = reader.GetNullableDoubleAttribute(FormatVersion.CompareTo(DocumentFormat.VERSION_3_52) < 0 ? ATTR.cone_voltage_obsolete : ATTR.explicit_cone_voltage);
            double? importedCompensationVoltage = reader.GetNullableDoubleAttribute(ATTR.explicit_compensation_voltage);
            double? importedDeclusteringPotential = reader.GetNullableDoubleAttribute(ATTR.explicit_declustering_potential);
            return new ExplicitTransitionGroupValues(importedCollisionEnergy, importedDriftTimeMsec, importedDriftTimeHighEnergyOffsetMsec, importedCCS, importedSLens, importedConeVoltage,
                importedDeclusteringPotential, importedCompensationVoltage);
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a peptide or molecule</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="isCustomMolecule">if true, we're reading a custom molecule, not a peptide</param>
        /// <returns>A new <see cref="PeptideDocNode"/></returns>
        private PeptideDocNode ReadPeptideXml(XmlReader reader, PeptideGroup group, bool isCustomMolecule)
        {
            int? start = reader.GetNullableIntAttribute(ATTR.start);
            int? end = reader.GetNullableIntAttribute(ATTR.end);
            string sequence = reader.GetAttribute(ATTR.sequence);
            string lookupSequence = reader.GetAttribute(ATTR.lookup_sequence);
            // If the group has no sequence, then this is a v0.1 peptide list or a custom ion
            if (group.Sequence == null)
            {
                // Ignore the start and end values
                start = null;
                end = null;
            }
            int missedCleavages = reader.GetIntAttribute(ATTR.num_missed_cleavages);
            // CONSIDER: Trusted value
            int? rank = reader.GetNullableIntAttribute(ATTR.rank);
            double? concentrationMultiplier = reader.GetNullableDoubleAttribute(ATTR.concentration_multiplier);
            double? internalStandardConcentration =
                reader.GetNullableDoubleAttribute(ATTR.internal_standard_concentration);
            string normalizationMethod = reader.GetAttribute(ATTR.normalization_method);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);
            var standardType = StandardType.FromName(reader.GetAttribute(ATTR.standard_type));
            double? importedRetentionTimeValue = reader.GetNullableDoubleAttribute(ATTR.explicit_retention_time);
            double? importedRetentionTimeWindow = reader.GetNullableDoubleAttribute(ATTR.explicit_retention_time_window);
            var importedRetentionTime = importedRetentionTimeValue.HasValue
                ? new ExplicitRetentionTimeInfo(importedRetentionTimeValue.Value, importedRetentionTimeWindow)
                : null;
            var annotations = Annotations.EMPTY;
            ExplicitMods mods = null, lookupMods = null;
            Results<PeptideChromInfo> results = null;
            TransitionGroupDocNode[] children = null;
            Adduct adduct = Adduct.EMPTY;
            var customMolecule = isCustomMolecule ? CustomMolecule.Deserialize(reader, out adduct) : null; // This Deserialize only reads attribures, doesn't advance the reader
            if (customMolecule != null)
            {
                if (DocumentMayContainMoleculesWithEmbeddedIons && string.IsNullOrEmpty(customMolecule.Formula) && customMolecule.MonoisotopicMass.IsMassH())
                {
                    // Defined by mass only, assume it's not massH despite how it may have been written
                    customMolecule = new CustomMolecule(
                        customMolecule.MonoisotopicMass.ChangeIsMassH(false),
                        customMolecule.AverageMass.ChangeIsMassH(false),
                        customMolecule.Name);
                }
            }
            Assume.IsTrue(DocumentMayContainMoleculesWithEmbeddedIons || adduct.IsEmpty); // Shouldn't be any charge info at the peptide/molecule level
            var peptide = isCustomMolecule ?
                new Peptide(customMolecule) :
                new Peptide(group as FastaSequence, sequence, start, end, missedCleavages, isDecoy);

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                if (reader.IsStartElement())
                    annotations = ReadAnnotations(reader, _stringPool);
                if (!isCustomMolecule)
                {
                    // CONSIDER(bspratt) the code as written actually can use static isotope labels
                    // label modifications, and this if clause could be removed - but Brendan wants proof of demand for this first
                    mods = ReadExplicitMods(reader, peptide);
                    SkipImplicitModsElement(reader);
                    lookupMods = ReadLookupMods(reader, lookupSequence);
                }
                results = ReadPeptideResults(reader);

                if (reader.IsStartElement(EL.precursor))
                {
                    // If this is an older small molecule file, clean up any problems with former data model
                    // where the "peptide" had the same formula as the precursor - and thus was not neutral
                    double massShift = 0;
                    if (isCustomMolecule && DocumentMayContainMoleculesWithEmbeddedIons)
                    {
                        peptide = HandlePre372CustomIonTransitionGroups(reader, peptide, ref massShift);
                    }
                    children = ReadTransitionGroupListXml(reader, peptide, mods, massShift);
                }
                else if (reader.IsStartElement(EL.selected_transitions))
                {
                    // Support for v0.1
                    if (reader.IsEmptyElement)
                        reader.Read();
                    else
                    {
                        reader.ReadStartElement(EL.selected_transitions);
                        children = ReadUngroupedTransitionListXml(reader, peptide, mods);
                        reader.ReadEndElement();
                    }
                }

                reader.ReadEndElement();
            }

            ModifiedSequenceMods sourceKey = null;
            if (lookupSequence != null)
                sourceKey = new ModifiedSequenceMods(lookupSequence, lookupMods);

            PeptideDocNode peptideDocNode = new PeptideDocNode(peptide, Settings, mods, sourceKey, standardType, rank,
                importedRetentionTime, annotations, results, children ?? new TransitionGroupDocNode[0], autoManageChildren);
            peptideDocNode = peptideDocNode
                .ChangeConcentrationMultiplier(concentrationMultiplier)
                .ChangeInternalStandardConcentration(internalStandardConcentration)
                .ChangeNormalizationMethod(NormalizationMethod.FromName(normalizationMethod));
            return peptideDocNode;
        }

        private ExplicitMods ReadLookupMods(XmlReader reader, string lookupSequence)
        {
            if (!reader.IsStartElement(EL.lookup_modifications))
                return null;
            reader.Read();
            string sequence = FastaSequence.StripModifications(lookupSequence);
            var mods = ReadExplicitMods(reader, new Peptide(sequence));
            reader.ReadEndElement();
            return mods;
        }

        private void SkipImplicitModsElement(XmlReader reader)
        {
            if (!reader.IsStartElement(EL.implicit_modifications))
                return;
            reader.Skip();
        }

        private ExplicitMods ReadExplicitMods(XmlReader reader, Peptide peptide)
        {
            IList<ExplicitMod> staticMods = null;
            TypedExplicitModifications staticTypedMods = null;
            IList<TypedExplicitModifications> listHeavyMods = null;
            bool isVariable = false;

            if (reader.IsStartElement(EL.variable_modifications))
            {
                staticTypedMods = ReadExplicitMods(reader, EL.variable_modifications,
                    EL.variable_modification, peptide, IsotopeLabelType.light);
                staticMods = staticTypedMods.Modifications;
                isVariable = true;
            }
            if (reader.IsStartElement(EL.explicit_modifications))
            {
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();

                    if (!isVariable)
                    {
                        if (reader.IsStartElement(EL.explicit_static_modifications))
                        {
                            staticTypedMods = ReadExplicitMods(reader, EL.explicit_static_modifications,
                                EL.explicit_modification, peptide, IsotopeLabelType.light);
                            staticMods = staticTypedMods.Modifications;
                        }
                        // For format version 0.2 and earlier it was not possible
                        // to have unmodified types.  The absence of a type simply
                        // meant it had no modifications.
                        else if (FormatVersion.CompareTo(DocumentFormat.VERSION_0_2) <= 0)
                        {
                            staticTypedMods = new TypedExplicitModifications(peptide,
                                IsotopeLabelType.light, new ExplicitMod[0]);
                            staticMods = staticTypedMods.Modifications;
                        }
                    }
                    listHeavyMods = new List<TypedExplicitModifications>();
                    while (reader.IsStartElement(EL.explicit_heavy_modifications))
                    {
                        var heavyMods = ReadExplicitMods(reader, EL.explicit_heavy_modifications,
                            EL.explicit_modification, peptide, IsotopeLabelType.heavy);
                        heavyMods = heavyMods.AddModMasses(staticTypedMods);
                        listHeavyMods.Add(heavyMods);
                    }
                    if (FormatVersion.CompareTo(DocumentFormat.VERSION_0_2) <= 0 && listHeavyMods.Count == 0)
                    {
                        listHeavyMods.Add(new TypedExplicitModifications(peptide,
                            IsotopeLabelType.heavy, new ExplicitMod[0]));
                    }
                    reader.ReadEndElement();
                }
            }
            if (staticMods == null && listHeavyMods == null)
                return null;

            listHeavyMods = (listHeavyMods != null ?
                listHeavyMods.ToArray() : new TypedExplicitModifications[0]);

            return new ExplicitMods(peptide, staticMods, listHeavyMods, isVariable);
        }

        private TypedExplicitModifications ReadExplicitMods(XmlReader reader, string name,
            string nameElMod, Peptide peptide, IsotopeLabelType labelTypeDefault)
        {
            if (!reader.IsStartElement(name))
                return new TypedExplicitModifications(peptide, labelTypeDefault, new ExplicitMod[0]);

            var typedMods = ReadLabelType(reader, labelTypeDefault);
            var listMods = new List<ExplicitMod>();

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                while (reader.IsStartElement(nameElMod))
                {
                    int indexAA = reader.GetIntAttribute(ATTR.index_aa);
                    string nameMod = reader.GetAttribute(ATTR.modification_name);
                    int indexMod = typedMods.Modifications.IndexOf(mod => Equals(nameMod, mod.Name));
                    if (indexMod == -1)
                        throw new InvalidDataException(string.Format(Resources.TransitionInfo_ReadTransitionLosses_No_modification_named__0__was_found_in_this_document, nameMod));
                    StaticMod modAdd = typedMods.Modifications[indexMod];
                    listMods.Add(new ExplicitMod(indexAA, modAdd));
                    // Consume tag
                    reader.Read();
                }
                reader.ReadEndElement();
            }
            return new TypedExplicitModifications(peptide, typedMods.LabelType, listMods.ToArray());
        }

        private Results<PeptideChromInfo> ReadPeptideResults(XmlReader reader)
        {
            if (reader.IsStartElement(EL.peptide_results))
                return ReadResults(reader, EL.peptide_result, ReadPeptideChromInfo);
            return null;
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionGroupDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="peptide">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <param name="deltaMass">Slight shift taken into account when translating pre-3.62 small molecule documents</param>
        /// <returns>A new array of <see cref="TransitionGroupDocNode"/></returns>
        private TransitionGroupDocNode[] ReadTransitionGroupListXml(XmlReader reader, Peptide peptide, ExplicitMods mods, double deltaMass)
        {
            var list = new List<TransitionGroupDocNode>();
            while (reader.IsStartElement(EL.precursor))
                list.Add(ReadTransitionGroupXml(reader, peptide, mods, deltaMass));
            return list.ToArray();
        }

        // Read the first-seen transition group and try to discern the actual neutral formula of the molecule
        // Needed since we actually stored the precursor description rather than the molecule description in v3.71 and earlier
        private Peptide HandlePre372CustomIonTransitionGroups(XmlReader reader, Peptide peptide, ref double massShift)
        {
            if (peptide.IsCustomMolecule)
            {
                var precursorCharge = reader.GetIntAttribute(ATTR.charge);
                var ionFormula = reader.GetAttribute(ATTR.ion_formula);
                if (ionFormula != null)
                {
                    ionFormula = ionFormula.Trim(); // We've seen tailing spaces in the wild
                }
                if (!string.IsNullOrEmpty(ionFormula))
                {
                    // Check to see if "peptide" formula is actually protonated - we used to treat base peptide
                    // molecule and first precursor as the same thing
                    string precursorFormula;
                    Adduct precursorAdduct;
                    Molecule precursorMol;
                    var ionFormulaHasAdduct = IonInfo.IsFormulaWithAdduct(ionFormula, out precursorMol,
                        out precursorAdduct, out precursorFormula);
                    if (ionFormulaHasAdduct &&
                        Equals(peptide.CustomMolecule.Formula, precursorAdduct.ApplyToFormula(precursorFormula)))
                    {
                        peptide = new Peptide(new CustomMolecule(precursorFormula, peptide.CustomMolecule.Name));
                    }
                    else if (Equals(peptide.CustomMolecule.Formula, ionFormula))
                    {
                        var declaredMz = reader.GetDoubleAttribute(ATTR.precursor_mz);
                        if (Math.Abs(peptide.CustomMolecule.MonoisotopicMass - Math.Abs(precursorCharge) * declaredMz) < .01)
                        {
                            // The "peptide" formula is actually the protonated precursor ion formula
                            var newFormula = Molecule.AdjustElementCount(ionFormula, "H", -precursorCharge); // Not L10N
                            peptide = new Peptide(new CustomMolecule(newFormula, peptide.CustomMolecule.Name));
                        }
                    }
                }
                if (string.IsNullOrEmpty(ionFormula))
                {
                    ionFormula = peptide.CustomMolecule.Formula;
                }
                if (string.IsNullOrEmpty(ionFormula))
                {
                    // Declared as masses only - disambiguate mass vs massH if possible
                    var declaredMz = reader.GetDoubleAttribute(ATTR.precursor_mz);
                    var adduct = Adduct.FromChargeProtonated(precursorCharge);
                    var mass = adduct.MassFromMz(declaredMz, peptide.CustomMolecule.MonoisotopicMass.MassType);
                    // Peptide m/z values got stored with electron mass added
                    var ionMass = mass + precursorCharge * BioMassCalc.MassProton;
                    var moleculeMass = peptide.CustomMolecule.MonoisotopicMass -
                                       precursorCharge * BioMassCalc.MassElectron;
                    if (Math.Abs(ionMass - moleculeMass) <= 5E-7)    // And they may have only been stored to 6 digits of precision
                    {
                        var mono = new TypedMass(moleculeMass - precursorCharge * BioMassCalc.MassProton, MassType.Monoisotopic);
                        moleculeMass = peptide.CustomMolecule.AverageMass -
                                       precursorCharge * BioMassCalc.MassElectron;
                        var avg = new TypedMass(moleculeMass - precursorCharge * BioMassCalc.MassProton, MassType.Average);
                        peptide = new Peptide(new CustomMolecule(mono, avg, peptide.CustomMolecule.Name));
                        // The difference between the mass of Hydrogen and the combined mass of a proton
                        // and an electron shows up in translating from m/z-only small molecules
                        massShift = BioMassCalc.MONOISOTOPIC.MassH - (BioMassCalc.MassProton + BioMassCalc.MassElectron);
                    }
                }
            }
            return peptide;
        }

        private TransitionGroupDocNode ReadTransitionGroupXml(XmlReader reader, Peptide peptide, ExplicitMods mods, double deltaMass)
        {
            var precursorCharge = reader.GetIntAttribute(ATTR.charge);
            var precursorAdduct = Adduct.FromChargeProtonated(precursorCharge);  // Read integer charge
            var typedMods = ReadLabelType(reader, IsotopeLabelType.light);

            int? decoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
            var explicitTransitionGroupValues = ReadExplicitTransitionValuesAttributes(reader);
            if (peptide.IsCustomMolecule)
            {
                var ionFormula = reader.GetAttribute(ATTR.ion_formula);
                if (ionFormula != null)
                {
                    ionFormula = ionFormula.Trim(); // We've seen trailing spaces in the wild
                }
                Molecule mol;
                string neutralFormula;
                Adduct adduct;
                var isFormulaWithAdduct = IonInfo.IsFormulaWithAdduct(ionFormula, out mol, out adduct, out neutralFormula);
                if (isFormulaWithAdduct)
                {
                    precursorAdduct = adduct;
                }
                else
                {
                    // No adduct given - try to figure it out from charge and mass
                    precursorAdduct = Adduct.NonProteomicProtonatedFromCharge(precursorCharge); // Assume (de)protonation
                    if (string.IsNullOrEmpty(ionFormula))
                    {
                        // Mass-only declarations sometimes don't distinguish mz from mass, so just declare charge
                        var declaredMz = reader.GetDoubleAttribute(ATTR.precursor_mz);
                        if (Math.Abs(declaredMz - peptide.CustomMolecule.MonoisotopicMass) < .001)
                        {
                            precursorAdduct = Adduct.FromChargeNoMass(precursorCharge);  // [M+], [M--] etc
                        }
                    }
                }
                if (DocumentMayContainMoleculesWithEmbeddedIons && !string.IsNullOrEmpty(ionFormula))
                {
                    // Old model - we may have encoded isotopes into the precursor's ion formula, and included the adduct's atoms in the molecule count
                    precursorAdduct = precursorAdduct.ChangeIsotopeLabels(BioMassCalc.MONOISOTOPIC.FindIsotopeLabelsInFormula(ionFormula));
                    if (!Equals(precursorAdduct.ApplyToFormula(peptide.CustomMolecule.Formula),
                        adduct.IsEmpty ? ionFormula : adduct.ApplyToFormula(neutralFormula)))
                    {
                        // Take precursor adduct as the difference between the precursor ion formula and the molecule ion formula
                        precursorAdduct =
                            Adduct.FromFormulaDiff(ionFormula, peptide.CustomMolecule.Formula, precursorCharge)
                                .ChangeIsotopeLabels(BioMassCalc.MONOISOTOPIC.FindIsotopeLabelsInFormula(ionFormula));
                    }
                    neutralFormula = peptide.CustomMolecule.Formula;
                }
                if (!string.IsNullOrEmpty(neutralFormula))
                {
                    var ionString = precursorAdduct.ApplyToFormula(neutralFormula);
                    var moleculeWithAdduct = precursorAdduct.ApplyToFormula(peptide.CustomMolecule.Formula);
                    Assume.IsTrue(Equals(ionString, moleculeWithAdduct), "Expected precursor ion formula to match parent molecule with adduct applied");  // Not L10N
                    if (DocumentMayContainMoleculesWithEmbeddedIons && !typedMods.LabelType.IsLight)
                    {
                        // Do we actually need to embed isotope info in the adduct, or is it handled by typedMods.LabelType?
                        try
                        {
                            string isotopicFormula;
                            Settings.GetPrecursorMass(typedMods.LabelType, peptide.CustomMolecule, typedMods, precursorAdduct.Unlabeled, out isotopicFormula);
                            if (Equals(moleculeWithAdduct, precursorAdduct.Unlabeled.ApplyToFormula(isotopicFormula)))
                            {
                                precursorAdduct = precursorAdduct.Unlabeled; // No need to be explicit about labels in the adduct
                            }
                        }
                        catch (InvalidDataException)
                        {
                            // Didn't find a precursor calculator for that isotope label type, so keep any isotopes in adduct
                        }
                    }
                }
                else if (DocumentMayContainMoleculesWithEmbeddedIons && !typedMods.LabelType.IsLight && string.IsNullOrEmpty(peptide.CustomMolecule.Formula))
                {
                    // Labeled, but described by mass only - express label effect in the adduct, preferably as a count of the declared ion label
                    var lightMz = precursorAdduct.ApplyToMass(peptide.CustomMolecule.MonoisotopicMass) - precursorCharge*deltaMass;
                    var heavyMz = peptide.CustomMolecule.ReadMonoisotopicMass(reader); // ReadMonoisotopicMass makes no assignments, it just alters read behavior based on type
                    var massDiff = heavyMz - lightMz;
                    if (Math.Abs(massDiff) < 1E-4)
                        massDiff = TypedMass.ZERO_MONO_MASSH;
                    precursorAdduct = precursorAdduct.ChangeIsotopeLabels(massDiff); // Declare as mass if we can't deduce a tidy label
                    if ((massDiff != 0) && typedMods.Modifications.Any())
                    {
                        var labelMassDiff = typedMods.Modifications.First().IonLabelMassDiff;
                        if (labelMassDiff != 0)
                        {
                            var n = Math.Round(massDiff / labelMassDiff);
                            if (Math.Abs(n * labelMassDiff - massDiff) < .001)
                            {
                                // Close enough to represent as "2N15" instead if the mass of two 15N's less two N's
                                var label = new Dictionary<string, int>();
                                foreach (var labelName in typedMods.Modifications.First().LabelNames)
                                {
                                    var nn = (int)n;
                                    var adductLabelName = Adduct.DICT_ADDUCT_ISOTOPE_NICKNAMES.FirstOrDefault(x => x.Value == labelName).Key;
                                    label[adductLabelName] = nn;
                                }
                                precursorAdduct = precursorAdduct.ChangeIsotopeLabels(label);
                            }
                        }
                    }
                }
            }
            var group = new TransitionGroup(peptide, precursorAdduct, typedMods.LabelType, false, decoyMassShift);
            var children = new TransitionDocNode[0];    // Empty until proven otherwise
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);

            if (reader.IsEmptyElement)
            {
                reader.Read();

                return new TransitionGroupDocNode(group,
                                                  Annotations.EMPTY,
                                                  Settings,
                                                  mods,
                                                  null,
                                                  explicitTransitionGroupValues,
                                                  null,
                                                  children,
                                                  autoManageChildren);
            }
            else
            {
                reader.ReadStartElement();
                var annotations = ReadAnnotations(reader, _stringPool);
                var libInfo = ReadTransitionGroupLibInfo(reader);
                var results = ReadTransitionGroupResults(reader);

                var nodeGroup = new TransitionGroupDocNode(group,
                                                  annotations,
                                                  Settings,
                                                  mods,
                                                  libInfo,
                                                  explicitTransitionGroupValues,
                                                  results,
                                                  children,
                                                  autoManageChildren);
                children = ReadTransitionListXml(reader, group, mods, nodeGroup.IsotopeDist);

                reader.ReadEndElement();

                return (TransitionGroupDocNode)nodeGroup.ChangeChildrenChecked(children);
            }
        }

        private TypedModifications ReadLabelType(XmlReader reader, IsotopeLabelType labelTypeDefault)
        {
            string typeName = reader.GetAttribute(ATTR.isotope_label);
            if (string.IsNullOrEmpty(typeName))
                typeName = labelTypeDefault.Name;
            var typedMods = Settings.PeptideSettings.Modifications.GetModificationsByName(typeName);
            if (typedMods == null)
                throw new InvalidDataException(string.Format(Resources.SrmDocument_ReadLabelType_The_isotope_modification_type__0__does_not_exist_in_the_document_settings, typeName));
            return typedMods;
        }

        private Results<TransitionGroupChromInfo> ReadTransitionGroupResults(XmlReader reader)
        {
            if (reader.IsStartElement(EL.precursor_results))
                return ReadResults(reader, EL.precursor_peak, ReadTransitionGroupChromInfo);
            return null;
        }

        /// <summary>
        /// Deserializes ungrouped transitions in v0.1 format from a <see cref="XmlReader"/>
        /// into an array of <see cref="TransitionGroupDocNode"/> objects with
        /// children <see cref="TransitionDocNode"/> from the XML correctly distributed.
        /// 
        /// There were no "heavy" transitions in v0.1, making this a matter of
        /// distributing multiple precursor charge states, though in most cases
        /// there will be only one.
        /// </summary>
        /// <param name="reader">The reader positioned on a &lt;transition&gt; start tag</param>
        /// <param name="peptide">A previously read <see cref="Peptide"/> instance</param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <returns>An array of <see cref="TransitionGroupDocNode"/> instances for
        ///         inclusion in a <see cref="PeptideDocNode"/> child list</returns>
        private TransitionGroupDocNode[] ReadUngroupedTransitionListXml(XmlReader reader, Peptide peptide, ExplicitMods mods)
        {
            TransitionInfo info = new TransitionInfo(this);
            TransitionGroup curGroup = null;
            List<TransitionDocNode> curList = null;
            var listGroups = new List<TransitionGroup>();
            var mapGroupToList = new Dictionary<TransitionGroup, List<TransitionDocNode>>();
            while (reader.IsStartElement(EL.transition))
            {
                // Read a transition tag.
                info.ReadXml(reader);

                // If the transition is not in the current group
                if (curGroup == null || curGroup.PrecursorAdduct != info.PrecursorAdduct)
                {
                    // Look for an existing group that matches
                    curGroup = null;
                    foreach (TransitionGroup group in listGroups)
                    {
                        if (group.PrecursorAdduct == info.PrecursorAdduct)
                        {
                            curGroup = group;
                            break;
                        }
                    }
                    if (curGroup != null)
                        curList = mapGroupToList[curGroup];
                    else
                    {
                        // No existing group matches, so create a new one
                        curGroup = new TransitionGroup(peptide, info.PrecursorAdduct, IsotopeLabelType.light);
                        curList = new List<TransitionDocNode>();
                        listGroups.Add(curGroup);
                        mapGroupToList.Add(curGroup, curList);
                    }
                }
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, peptide.Length);
                Transition transition = new Transition(curGroup, info.IonType,
                    offset, info.MassIndex, info.ProductAdduct);

                // No heavy transition support in v0.1, and no full-scan filtering
                var massH = Settings.GetFragmentMass(null, mods, transition, null);

                curList.Add(new TransitionDocNode(transition, info.Losses, massH, TransitionDocNode.TransitionQuantInfo.DEFAULT));
            }

            // Use collected information to create the DocNodes.
            var list = new List<TransitionGroupDocNode>();
            foreach (TransitionGroup group in listGroups)
            {
                list.Add(new TransitionGroupDocNode(group, Annotations.EMPTY,
                    Settings, mods, null, ExplicitTransitionGroupValues.EMPTY, null, mapGroupToList[group].ToArray(), true));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionDocNode"/> objects from
        /// a <see cref="TransitionDocNode"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <param name="isotopeDist">Isotope peak distribution to use for assigning M+N m/z values</param>
        /// <returns>A new array of <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode[] ReadTransitionListXml(XmlReader reader, 
            TransitionGroup group, ExplicitMods mods, IsotopeDistInfo isotopeDist)
        {
            var list = new List<TransitionDocNode>();
            if (reader.IsStartElement(EL.transition_data))
            {
                string strContent = reader.ReadElementString();
                byte[] data = Convert.FromBase64String(strContent);
                var transitionData = new SkylineDocumentProto.Types.TransitionData();
                transitionData.MergeFrom(data);
                foreach (var transitionProto in transitionData.Transitions)
                {
                    list.Add(TransitionDocNode.FromTransitionProto(_stringPool, Settings, group, mods, isotopeDist, transitionProto));
                }
            }
            else
            {
                while (reader.IsStartElement(EL.transition))
                    list.Add(ReadTransitionXml(reader, group, mods, isotopeDist));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes a single <see cref="TransitionDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a transition</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <param name="isotopeDist">Isotope peak distribution to use for assigning M+N m/z values</param>
        /// <returns>A new <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode ReadTransitionXml(XmlReader reader, TransitionGroup group,
            ExplicitMods mods, IsotopeDistInfo isotopeDist)
        {
            TransitionInfo info = new TransitionInfo(this);

            // Read all the XML attributes before the reader advances through the elements
            info.ReadXmlAttributes(reader);
            var isPrecursor = Transition.IsPrecursor(info.IonType);
            var isCustom = Transition.IsCustom(info.IonType, group);
            CustomMolecule customMolecule = null;
            Adduct adduct = Adduct.EMPTY;
            if (isCustom)
            {
                if (info.MeasuredIon != null)
                    customMolecule = info.MeasuredIon.SettingsCustomIon;
                else if (isPrecursor)
                    customMolecule = group.CustomMolecule;
                else
                {
                    customMolecule = CustomMolecule.Deserialize(reader, out adduct);
                    if (DocumentMayContainMoleculesWithEmbeddedIons && string.IsNullOrEmpty(customMolecule.Formula) && customMolecule.MonoisotopicMass.IsMassH())
                    {
                        // Defined by mass only, assume it's not massH despite how it may have been written
                        customMolecule = new CustomMolecule(customMolecule.MonoisotopicMass.ChangeIsMassH(false), customMolecule.AverageMass.ChangeIsMassH(false),
                            customMolecule.Name);
                    }
                }
            }
            double? declaredProductMz;
            info.ReadXmlElements(reader, out declaredProductMz);

            if (adduct.IsEmpty)
            {
                adduct = info.ProductAdduct;
                var isPre362NonReporterCustom = DocumentMayContainMoleculesWithEmbeddedIons && customMolecule != null &&
                                                 !(customMolecule is SettingsCustomIon); // Leave reporter ions alone
                if (isPre362NonReporterCustom && adduct.IsProteomic)
                {
                    adduct = Adduct.NonProteomicProtonatedFromCharge(adduct.AdductCharge);
                }
                // Watch all-mass declaration with mz same as mass with a charge-only adduct, which older versions don't describe succinctly
                if (!isPrecursor && isPre362NonReporterCustom &&
                    Math.Abs(declaredProductMz.Value - customMolecule.MonoisotopicMass) < .001)
                {
                    string newFormula = null;
                    if (!string.IsNullOrEmpty(customMolecule.Formula) &&
                        Math.Abs(customMolecule.MonoisotopicMass - Math.Abs(adduct.AdductCharge) * declaredProductMz.Value) < .01)
                    {
                        // Adjust hydrogen count to get a molecular mass that makes sense for charge and mz
                        newFormula = Molecule.AdjustElementCount(customMolecule.Formula, "H", -adduct.AdductCharge); // Not L10N
                    }
                    if (!string.IsNullOrEmpty(newFormula))
                    {
                        customMolecule = new CustomMolecule(newFormula, customMolecule.Name);
                    }
                    else
                    {
                        // All we can really say about the adduct is that it has a charge
                        adduct = Adduct.FromChargeNoMass(adduct.AdductCharge);
                    }
                }
            }
            else
            {
                // We parsed an adduct out of the molecule description, as in older versions - make sure it agrees with parsed charge
                // ReSharper disable once PossibleNullReferenceException
                Assume.IsTrue(adduct.AdductCharge == info.ProductAdduct.AdductCharge);
            }

            Transition transition;
            if (isCustom)
            {
                transition = new Transition(group, isPrecursor ? group.PrecursorAdduct : adduct, info.MassIndex,
                    customMolecule, info.IonType);
            }
            else if (isPrecursor)
            {
                transition = new Transition(group, info.IonType, group.Peptide.Length - 1, info.MassIndex,
                    group.PrecursorAdduct, info.DecoyMassShift);
            }
            else
            {
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, group.Peptide.Length);
                transition = new Transition(group, info.IonType, offset, info.MassIndex, adduct, info.DecoyMassShift);
            }

            var losses = info.Losses;
            var mass = Settings.GetFragmentMass(group, mods, transition, isotopeDist);

            var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(transition, losses, isotopeDist);

            if (group.DecoyMassShift.HasValue && !info.DecoyMassShift.HasValue)
                throw new InvalidDataException(Resources.SrmDocument_ReadTransitionXml_All_transitions_of_decoy_precursors_must_have_a_decoy_mass_shift);
            return new TransitionDocNode(transition, info.Annotations, losses,
                mass, new TransitionDocNode.TransitionQuantInfo(isotopeDistInfo, info.LibInfo, info.Quantitative), info.Results);
        }
    }
}
