"""Enumeration wire contract regression checks. (c) 2026 Lattice AI LLC."""
import unittest
from urllib.parse import urlparse, parse_qs
from mymorningnews_sdk import EnumerationQuery, EnumerationResult, MyMorningNewsClient
from test_platform import Transport


class EnumerationTest(unittest.TestCase):
    def test_former_array_routes(self):
        transport = Transport()
        api = MyMorningNewsClient('https://example.test', session=transport)
        query = EnumerationQuery(page_number=2, page_size=1, order='CreatedAscending')
        transport.reply = dict(success=True, objects=[{'id': 'second'}], pageNumber=2, pageSize=1, totalRecords=3, totalPages=3, totalMs=0)
        calls = [lambda **q: api.devices.list(**q), lambda **q: api.feed_tokens.list(**q),
                 lambda **q: api.topics.dimensions('t', **q), lambda **q: api.topics.constraints('t', **q),
                 lambda **q: api.topics.recipients('t', **q), lambda **q: api.topics.search_providers('t', **q),
                 lambda **q: api.runs.stages('r', **q), lambda **q: api.runs.queries('r', **q),
                 lambda **q: api.runs.results('r', **q), lambda **q: api.runs.tool_calls('r', **q),
                 lambda **q: api.templates.versions('t', **q), lambda **q: api.prompts.versions('p', **q),
                 lambda **q: api.vault.usage('v', **q), lambda **q: api.model_endpoints.models('m', **q)]
        for call in calls:
            page = call(**query.to_keyword_arguments())
            self.assertIsInstance(page, EnumerationResult)
            self.assertEqual(1, len(page.objects))
            self.assertEqual(3, page.total_records)
            sent = parse_qs(urlparse(transport.sent[-1].url).query)
            self.assertEqual(['2'], sent['pageNumber'])
            self.assertEqual(['1'], sent['pageSize'])
            self.assertEqual(['CreatedAscending'], sent['order'])

    def test_rejects_bare_arrays(self):
        with self.assertRaises(TypeError):
            EnumerationResult.from_dict([])

    def test_bounds(self):
        query = EnumerationQuery(page_number=-2, page_size=5000).to_keyword_arguments()
        self.assertEqual(1, query['page_number'])
        self.assertEqual(1000, query['page_size'])
