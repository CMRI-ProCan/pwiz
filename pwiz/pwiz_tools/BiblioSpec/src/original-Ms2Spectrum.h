//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

/*
 *  Ms2Spectrum class is an extension of Specrum
 */

#ifndef MS2SPECTRUM_H
#define MS2SPECTRUM_H

#include "original-Spectrum.h"

//using namespace std;

class Ms2Spectrum : public Spectrum
{
 public:
  Ms2Spectrum() { data.type = MS2; }
};

#endif //MS2SPECTRUM_H
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
