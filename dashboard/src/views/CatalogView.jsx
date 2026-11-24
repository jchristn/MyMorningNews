import { Route, Routes } from 'react-router-dom';
import CatalogBrowser from './catalog/CatalogBrowser.jsx';
import { CatalogIssuePage, CatalogIssuesPage, PublicationIssuesPage } from './catalog/CatalogIssues.jsx';

export default function CatalogView() {
  return (
    <Routes>
      <Route index element={<CatalogBrowser />} />
      <Route path="issues" element={<CatalogIssuesPage />} />
      <Route path=":publicationId/issues" element={<PublicationIssuesPage />} />
      <Route path=":publicationId/issues/:issueId" element={<CatalogIssuePage />} />
    </Routes>
  );
}
