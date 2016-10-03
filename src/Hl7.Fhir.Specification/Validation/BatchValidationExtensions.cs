﻿using Hl7.ElementModel;
using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hl7.Fhir.Validation
{
    internal static class BatchValidationExtensions
    {
        public static OperationOutcome Combine(this Validator validator, BatchValidationMode mode, IElementNavigator instance, IEnumerable<Func<OperationOutcome>> validations)
        {
            if (validations.Count() == 0)
                return new OperationOutcome();

            if (validations.Count() == 1)
            {
                // To not pollute the output if there's just a single input, just add it to the output
                return validations.First()();
            }

            OperationOutcome combinedResult = new OperationOutcome();

            var modeLabel = mode == BatchValidationMode.All ? "ALL" : "ANY";
            validator.Trace(combinedResult, $"Combination of {validations.Count()} child validation runs, {modeLabel} must succeed", Issue.PROCESSING_PROGRESS, instance);

            int failures = 0;
            int successes = 0;

            List<OperationOutcome> results = new List<OperationOutcome>();

            // Run the given validations one by one, short circuiting when ANY success is enough
            foreach (var validation in validations)
            {
                var result = validation();
                results.Add(result);

                if (result.Success)
                    successes += 1;
                else
                    failures += 1;

                if (mode == BatchValidationMode.Any && successes > 0) break;       // shortcut evaluation
            }

            // Did we have success overall?
            bool success = mode == BatchValidationMode.Any && successes > 0 ||
                            mode == BatchValidationMode.All && failures == 0 ||
                            mode == BatchValidationMode.Once && successes == 1;

            // If the batch validation is a failure, or we simply want to trace all results,
            // add details information about each of the validation runs in the batch
            if (success == false || validator.Settings.Trace)
            {
                // Now, build final report
                for (var index = 0; index < results.Count; index++)
                {
                    var result = results[index];
                    combinedResult.Info($"Report {index}: {(result.Success ? "SUCCESS" : "FAILURE")}", Issue.PROCESSING_PROGRESS, instance);

                    if (success)
                    {
                        // We'd like to include all results of the combined reports, but if the total result is a success,
                        // any errors in failing runs should just be informational
                        if (!result.Success) result.MakeInformational();
                    }

                    combinedResult.Include(result);
                }
            }

            if (success)
                validator.Trace(combinedResult, "Combined validation succeeded", Issue.PROCESSING_PROGRESS, instance);
            else
                combinedResult.Info($"Combined validation failed, {failures} child validation runs failed, {successes} succeeded", Issue.PROCESSING_PROGRESS, instance);

            return combinedResult;
        }

    }
}
