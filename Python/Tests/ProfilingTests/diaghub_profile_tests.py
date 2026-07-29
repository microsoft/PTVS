import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest


BOOTSTRAP_PATH = sys.argv[1]


def load_bootstrap(path):
    spec = importlib.util.spec_from_file_location("diaghub_profile", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class DiagnosticsHubProfileTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bootstrap = load_bootstrap(BOOTSTRAP_PATH)

    def test_supported_versions(self):
        self.assertFalse(self.bootstrap._is_supported_version((3, 8)))
        self.assertTrue(self.bootstrap._is_supported_version((3, 9)))
        self.assertTrue(self.bootstrap._is_supported_version((3, 14)))
        self.assertFalse(self.bootstrap._is_supported_version((3, 15)))

    def test_target_validation(self):
        self.assertFalse(self.bootstrap._has_valid_target([]))
        self.assertFalse(self.bootstrap._has_valid_target(["-m"]))
        self.assertTrue(self.bootstrap._has_valid_target(["script.py"]))
        self.assertTrue(self.bootstrap._has_valid_target(["-m", "module"]))

    def test_profiled_arguments_round_trip(self):
        arguments = ["script.py", "value with spaces", "--flag=value"]
        old_value = os.environ.get(self.bootstrap._TARGET_ARGUMENTS)
        try:
            os.environ[self.bootstrap._TARGET_ARGUMENTS] = json.dumps(arguments)
            self.assertEqual(arguments, self.bootstrap._get_profiled_arguments())
            os.environ[self.bootstrap._TARGET_ARGUMENTS] = "{}"
            self.assertIsNone(self.bootstrap._get_profiled_arguments())
        finally:
            if old_value is None:
                os.environ.pop(self.bootstrap._TARGET_ARGUMENTS, None)
            else:
                os.environ[self.bootstrap._TARGET_ARGUMENTS] = old_value

    def test_script_execution_preserves_arguments(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            output = Path(temp_dir, "result.json")
            script = Path(temp_dir, "target.py")
            script.write_text(
                "import json, os, sys\n"
                "with open(os.environ['PTVS_TEST_OUTPUT'], 'w') as stream:\n"
                "    json.dump({'argv': sys.argv}, stream)\n",
                encoding="utf-8",
            )

            old_argv = sys.argv[:]
            old_output = os.environ.get("PTVS_TEST_OUTPUT")
            try:
                os.environ["PTVS_TEST_OUTPUT"] = str(output)
                self.bootstrap._run_target([str(script), "value with spaces"])
            finally:
                sys.argv[:] = old_argv
                if old_output is None:
                    os.environ.pop("PTVS_TEST_OUTPUT", None)
                else:
                    os.environ["PTVS_TEST_OUTPUT"] = old_output

            result = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual([str(script), "value with spaces"], result["argv"])

    def test_module_execution_matches_python_m(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            output = Path(temp_dir, "result.json")
            module = Path(temp_dir, "sample_module.py")
            module.write_text(
                "import json, os, sys\n"
                "with open(os.environ['PTVS_TEST_OUTPUT'], 'w') as stream:\n"
                "    json.dump({'argv': sys.argv, 'path0': sys.path[0]}, stream)\n",
                encoding="utf-8",
            )

            old_argv = sys.argv[:]
            old_path = sys.path[:]
            old_directory = os.getcwd()
            old_output = os.environ.get("PTVS_TEST_OUTPUT")
            try:
                os.chdir(temp_dir)
                os.environ["PTVS_TEST_OUTPUT"] = str(output)
                self.bootstrap._run_target(["-m", "sample_module", "argument"])
            finally:
                os.chdir(old_directory)
                sys.argv[:] = old_argv
                sys.path[:] = old_path
                if old_output is None:
                    os.environ.pop("PTVS_TEST_OUTPUT", None)
                else:
                    os.environ["PTVS_TEST_OUTPUT"] = old_output

            result = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual("sample_module.py", Path(result["argv"][0]).name)
            self.assertEqual(["argument"], result["argv"][1:])
            self.assertEqual(str(Path(temp_dir)), result["path0"])


if __name__ == "__main__":
    unittest.main(argv=[sys.argv[0]])
