//
// FeatureDetectorPeakel.hpp
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and 
// limitations under the License.
//
       

#ifndef _FEATUREDETECTORPEAKEL_HPP_
#define _FEATUREDETECTORPEAKEL_HPP_


#include "FeatureDetector.hpp"
#include "PeakExtractor.hpp"
#include "PeakelGrower.hpp"
#include "PeakelPicker.hpp"


namespace pwiz {
namespace analysis {


///
/// FeatureDetectorPeakel implements a 'template method', delegating to 'strategies'
/// encapsulated by the following interfaces:
///   PeakExtractor
///   PeakelGrower
///   PeakelPicker
///
class PWIZ_API_DECL FeatureDetectorPeakel : public FeatureDetector
{
    public:

    typedef pwiz::msdata::MSData MSData;

    FeatureDetectorPeakel(boost::shared_ptr<PeakExtractor> peakExtractor,
                          boost::shared_ptr<PeakelGrower> peakelGrower,
                          boost::shared_ptr<PeakelPicker> peakelPicker);

    virtual void detect(const MSData& msd, FeatureField& result) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
};


//
// TODO:  convenient instantiation, with Config structs
//        filtering on mz/rt -- ok to use SpectrumList_Filter for RT filter?
//          -- handle m/z window internally -- we want full arrays for noise calculation
// 


} // namespace analysis
} // namespace pwiz


#endif // _FEATUREDETECTORPEAKEL_HPP_

